using ACMESharp.Authorizations;
using ACMESharp.Protocol;
using LetsEncryptManager.Core.Account;
using LetsEncryptManager.Core.CertificateOperations;
using LetsEncryptManager.Core.CertificateStore;
using LetsEncryptManager.Core.Challenges;
using LetsEncryptManager.Core.Configuration;
using Microsoft.Extensions.Logging;
using PKISharp.SimplePKI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core
{
    public class CertRenewer
    {
        private AcmeProtocolClient? client;
        private readonly ILogger logger;
        private readonly CertRenewerConfig config;
        private readonly IAccountStore accountStore;
        private readonly IDnsChallengeHandler dnsHandler;
        private readonly ICertificateStore certStore;
        private Stopwatch stopwatch;

        public CertRenewer(ManagerConfig config,
            IAccountStore accountStore,
            IDnsChallengeHandler dnsHandler,
            ICertificateStore certStore,
            ILogger<CertRenewer> logger)
        {
            this.logger = logger;
            this.config = new CertRenewerConfig(config.CertContactEmail, config.CertificateAuthorityUrl);
            this.accountStore = accountStore;
            this.dnsHandler = dnsHandler;
            this.certStore = certStore;
            this.stopwatch = new Stopwatch();
        }

        public async Task RenewCertificate(string certName, params string[] hostnames)
        {
            this.stopwatch.Restart();

            await EnsureClient();

            if(this.client == null)
            {
                throw new Exception("EnsureClient failed to ensure client had a value, check configuration/permissions");
            }

            if (hostnames.Count() == 0)
            {
                throw new ArgumentException(nameof(hostnames), "One or more hostnames must be provided for the certificate");
            }

            var order = await client.CreateOrderAsync(hostnames);
            logger.LogInformation("[{0}]@{1}ms Created order: {2} ", certName, stopwatch.ElapsedMilliseconds, order.OrderUrl);

            await PerformChallengesAsync(order);
            logger.LogInformation("[{0}]@{1}ms Challenges complete", certName, stopwatch.ElapsedMilliseconds);

            // At this point all authorizations should be valid
            var (certCsr, keys) = this.GenerateCertificateRequest(hostnames);
            order = await client.FinalizeOrderAsync(order.Payload.Finalize, certCsr);
            logger.LogInformation("[{0}]@{1}ms Order finalized", certName, stopwatch.ElapsedMilliseconds);

            if(order.Payload.Status != "valid")
            {
                throw new Exception("Order isn't valid: " + order.Payload.Error);
            }

            logger.LogInformation("[{0}]@{1}ms Fetching cert and generating PFX", certName, stopwatch.ElapsedMilliseconds);
            var certBytes = await client.GetOrderCertificateAsync(order);
            var privateBytes = keys.PrivateKey.Export(PkiEncodingFormat.Pem);
            var pfx = CertExporter.ExportPfx(certBytes, privateBytes);

            await this.certStore.StorePfxCertificateAsync(certName, pfx);
            logger.LogInformation("[{0}]@{1}ms Saved certificate to '{2}'", certName, stopwatch.ElapsedMilliseconds, certStore.GetType().Name);

            stopwatch.Stop();
        }

        private async Task PerformChallengesAsync(OrderDetails order)
        {
            var exceptions = new List<Exception>();

            // Could be multiple authorizations if there are multiple distinct domains specified in the cert
            foreach (var authUrl in order.Payload.Authorizations)
            {
                var auth = await client.GetAuthorizationDetailsAsync(authUrl);

                var dnsChallenge = auth.Challenges.FirstOrDefault(c => c.Type == Dns01ChallengeValidationDetails.Dns01ChallengeType);

                if (dnsChallenge == null)
                {
                    exceptions.Add(new Exception("No DNS challenges for this authorization: " + authUrl));
                    continue;
                }

                var cd = (Dns01ChallengeValidationDetails)AuthorizationDecoder.DecodeChallengeValidation(
                    auth,
                    dnsChallenge.Type,
                    client.Signer);

                // Create requested DNS record, keep reference to remove after challenge passes
                logger.LogInformation("[{0}]@{1}ms Creating DNS record '{2}'", auth.Identifier, stopwatch.ElapsedMilliseconds, cd.DnsRecordName);
                var createdRecord = await this.dnsHandler.HandleAsync(cd.DnsRecordType, cd.DnsRecordName, cd.DnsRecordValue);
                logger.LogInformation("[{0}]@{1}ms Created DNS record successfully", auth.Identifier, stopwatch.ElapsedMilliseconds);

                var retries = 0;

                do
                {
                    Thread.Sleep(1000 * retries);

                    logger.LogInformation("[{0}]@{1}ms Answering challenge", auth.Identifier, stopwatch.ElapsedMilliseconds);
                    dnsChallenge = await client.AnswerChallengeAsync(dnsChallenge.Url);
                    auth = await client.GetAuthorizationDetailsAsync(authUrl);

                    retries++;
                } while (auth.Status != "valid" && retries < 5);

                if (auth.Status != "valid")
                {
                    exceptions.Add(new Exception($"Challenge validation was unsuccessful: [{dnsChallenge.Status}]{dnsChallenge.Error}\r\nAuthStatus: [{auth.Status}]"));
                }

                logger.LogInformation("[{0}]@{1}ms Removing DNS record '{2}'", auth.Identifier, stopwatch.ElapsedMilliseconds, cd.DnsRecordName);
                await createdRecord.CleanAsync();
                logger.LogInformation("[{0}]@{1}ms Removed DNS record successfully", auth.Identifier, stopwatch.ElapsedMilliseconds);
            }

            if (exceptions.Any())
            {
                throw new AggregateException("One or more exceptions occured while authorizing the request", exceptions);
            }
        }

        private (byte[] csr, PkiKeyPair keys) GenerateCertificateRequest(IEnumerable<string> hostnames)
        {
            using var ms = new MemoryStream();
            var keyPair = PkiKeyPair.GenerateRsaKeyPair(2048);
 
            var firstDns = hostnames.First();
            var csr = new PkiCertificateSigningRequest($"CN={firstDns}", keyPair, PkiHashAlgorithm.Sha256);
            csr.CertificateExtensions.Add(PkiCertificateExtension.CreateDnsSubjectAlternativeNames(hostnames));
            var certCsr = csr.ExportSigningRequest(PkiEncodingFormat.Der);

            return (certCsr, keyPair);
        }

        private async Task EnsureClient()
        {
            var caUri = new Uri(config.CertAuthorityUrl);

            var account = await accountStore.GetAccountAsync();

            if(account != null && account.Account.Kid.Contains(caUri.Host) == false)
            {
                logger.LogWarning("Fetched account KID doesn't contain CA host, ignoring fetched account");
                account = null;
            }

            var client = new AcmeProtocolClient(caUri, null, account?.Account, account?.Signer, usePostAsGet: true, logger: logger);
            client.Directory = await client.GetDirectoryAsync();

            // get nonce, used to communicate w/ server
            await client.GetNonceAsync();

            if (account == null)
            {
                // make request to create account
                var contactEmails = new[] { "mailto:" + config.ContactEmail };
                var newAccount = await client.CreateAccountAsync(contactEmails, termsOfServiceAgreed: true);
                var accountKey = new AccountKey
                {
                    KeyType = client.Signer.JwsAlg,
                    KeyExport = client.Signer.Export()
                };

                await accountStore.StoreAccountAsync(newAccount, accountKey);
                client.Account = newAccount;
            }

            this.client = client;
        }
    }
}
