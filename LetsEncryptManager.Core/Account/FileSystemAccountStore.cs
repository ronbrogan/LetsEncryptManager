using ACMESharp.Protocol;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Account
{
    public class FileSystemAccountStore : IAccountStore
    {
        private readonly string root;

        private string AccountDetailsFile => Path.Combine(this.root, "account.json");
        private string AccountKeyFile => Path.Combine(this.root, "account.secret");
        

        public FileSystemAccountStore(string root)
        {
            this.root = root;
            Directory.CreateDirectory(root);
        }

        public async Task<AuthorizedAccount?> GetAccountAsync()
        {
            if(File.Exists(AccountDetailsFile) && File.Exists(AccountKeyFile))
            {
                var deetsJson = File.ReadAllText(AccountDetailsFile);
                var deets = JsonConvert.DeserializeObject<AccountDetails>(deetsJson);

                var keyJson = File.ReadAllText(AccountKeyFile);
                var key = JsonConvert.DeserializeObject<AccountKey>(keyJson);

                return new AuthorizedAccount(deets, key.GenerateTool());
            }

            return null;
        }

        public Task StoreAccountAsync(AccountDetails details, AccountKey key)
        {
            File.WriteAllText(AccountDetailsFile, JsonConvert.SerializeObject(details));
            File.WriteAllText(AccountKeyFile, JsonConvert.SerializeObject(key));
            return Task.CompletedTask;
        }
    }
}
