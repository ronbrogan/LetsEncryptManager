using ACMESharp.Authorizations;
using ACMESharp.Protocol;
using LetsEncryptManager.Core.Account;
using LetsEncryptManager.Core.CertificateStore;
using LetsEncryptManager.Core.Challenges;
using LetsEncryptManager.Core.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PKISharp.SimplePKI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
        private readonly DnsProviderFactory dnsProviderFactory;
        private readonly ICertificateStore certStore;
        private Stopwatch stopwatch;

        public CertRenewer(ManagerConfig config,
            IAccountStore accountStore,
            DnsProviderFactory dnsProviderFactory,
            ICertificateStore certStore,
            ILogger<CertRenewer> logger)
        {
            this.logger = logger;
            this.config = new CertRenewerConfig(config.CertContactEmail, config.CertificateAuthorityUrl);
            this.accountStore = accountStore;
            this.dnsProviderFactory = dnsProviderFactory;
            this.certStore = certStore;
            this.stopwatch = new Stopwatch();
        }

        public async Task RenewCertificate(string certName, KnownCertificatesConfigEntry config)
        {
            var hostnames = config.Hostnames;

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

            await PerformChallengesAsync(order, config);
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
            using var cert = new X509Certificate2(certBytes);
            var pkiCert = PkiCertificate.From(cert);

            var pfx = pkiCert.Export(PkiArchiveFormat.Pkcs12, keys.PrivateKey);

            await this.certStore.StorePfxCertificateAsync(certName, pfx);
            logger.LogInformation("[{0}]@{1}ms Saved certificate to '{2}'", certName, stopwatch.ElapsedMilliseconds, certStore.GetType().Name);

            stopwatch.Stop();
        }

        private async Task PerformChallengesAsync(OrderDetails order, KnownCertificatesConfigEntry config)
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

                var dnsHandler = this.dnsProviderFactory.GetDnsProvider(config.DnsProvider);

                // Create requested DNS record, keep reference to remove after challenge passes
                logger.LogInformation("[{0}]@{1}ms Creating DNS record '{2}'", auth.Identifier.Value, stopwatch.ElapsedMilliseconds, cd.DnsRecordName);
                var createdRecord = await dnsHandler.HandleAsync(cd.DnsRecordType, cd.DnsRecordName, cd.DnsRecordValue, config);
                logger.LogInformation("[{0}]@{1}ms Created DNS record successfully", auth.Identifier.Value, stopwatch.ElapsedMilliseconds);

                try
                {
                    await Task.Delay(2000);

                    logger.LogInformation("[{0}]@{1}ms Answering challenge", auth.Identifier.Value, stopwatch.ElapsedMilliseconds);
                    dnsChallenge = await client.AnswerChallengeAsync(dnsChallenge.Url);

                    var retries = 1;
                    do
                    {
                        await Task.Delay(1000 * retries);

                        try
                        {

                            auth = await client.GetAuthorizationDetailsAsync(authUrl);
                            logger.LogInformation("[{0}]@{1}ms challenge status: {2}", auth.Identifier.Value, stopwatch.ElapsedMilliseconds, auth.Status);
                        }
                        catch (Exception e)
                        {
                            exceptions.Add(e);
                            logger.LogError(e, "[{0}]@{1}ms challenge exception dns: {2}, {3}, auth: {4}", auth.Identifier.Value, stopwatch.ElapsedMilliseconds, dnsChallenge.Error, dnsChallenge.Url, auth.Status);
                        }

                        if(auth.Status == "invalid")
                        {
                            var authJson = JsonConvert.SerializeObject(auth, Formatting.Indented);
                            logger.LogInformation("[{0}]@{1}ms challenge is invalid: {2}", auth.Identifier.Value, stopwatch.ElapsedMilliseconds, authJson);
                            break;
                        }

                        retries++;
                    } while (auth.Status != "valid" && retries <= 5);

                    if (auth.Status != "valid")
                    {
                        exceptions.Add(new Exception($"Challenge validation was unsuccessful: [{dnsChallenge.Status}]{dnsChallenge.Error}\r\nAuthStatus: [{auth.Status}]"));
                    }
                }
                finally
                {
                    logger.LogInformation("[{0}]@{1}ms Removing DNS record '{2}'", auth.Identifier.Value, stopwatch.ElapsedMilliseconds, cd.DnsRecordName);
                    await createdRecord.CleanAsync();
                    logger.LogInformation("[{0}]@{1}ms Removed DNS record successfully", auth.Identifier.Value, stopwatch.ElapsedMilliseconds);
                }
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
