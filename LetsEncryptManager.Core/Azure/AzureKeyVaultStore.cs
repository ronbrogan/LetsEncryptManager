using ACMESharp.Protocol;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Secrets;
using LetsEncryptManager.Core.Account;
using LetsEncryptManager.Core.Configuration;
using Newtonsoft.Json;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.CertificateStore
{
    public partial class AzureKeyVaultStore : ICertificateStore, IAccountStore
    {
        private const string AccountDetailsSecret = "AcmeAccount";
        private const string AccountKeySecret = "AcmeAccountSecret";

        private CertificateClient certClient;
        private SecretClient secretClient;

        public AzureKeyVaultStore(ManagerConfig config)
        {
            var cred = new DefaultAzureCredential();
            this.certClient = new CertificateClient(new Uri(config.KeyVaultUrl), cred);
            this.secretClient = new SecretClient(new Uri(config.KeyVaultUrl), cred);
        }

        public async Task<AuthorizedAccount?> GetAccountAsync()
        {
            var account = await this.GetSecretOrNull(AccountDetailsSecret);
            var accountKey = await this.GetSecretOrNull(AccountKeySecret);

            if (account != null && accountKey != null)
            {
                var deets = JsonConvert.DeserializeObject<AccountDetails>(account);
                var key = JsonConvert.DeserializeObject<AccountKey>(accountKey);

                return new AuthorizedAccount(deets, key.GenerateTool());
            }

            return null;
        }

        public async Task StoreAccountAsync(AccountDetails details, AccountKey key)
        {
            await this.secretClient.SetSecretAsync(AccountDetailsSecret, JsonConvert.SerializeObject(details));
            await this.secretClient.SetSecretAsync(AccountKeySecret, JsonConvert.SerializeObject(key));
        }

        public async Task<CertInfo?> GetCertInfo(string identifier)
        {
            try
            {
                var cert = await certClient.GetCertificateAsync(identifier);
                var x509 = new X509Certificate2(cert.Value.Cer);

                return new CertInfo(identifier, x509);
            }
            catch (RequestFailedException e)
            {
                if (e.Status != 404)
                    throw;
            }

            return null;
        }

        public async Task StorePfxCertificateAsync(string identifier, byte[] pfx)
        {
            var import = new ImportCertificateOptions(identifier, pfx);

            await certClient.ImportCertificateAsync(import);
        }

        private async Task<string?> GetSecretOrNull(string secretName)
        {
            try
            {
                var account = await secretClient.GetSecretAsync(secretName);
                return account.Value.Value;
            }
            catch (RequestFailedException e)
            {
                if (e.Status != 404)
                    throw;
            }

            return null;
        }
    }
}
