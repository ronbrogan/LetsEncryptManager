using ACMESharp.Protocol;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Account
{
    public interface IAccountStore
    {
        Task<AuthorizedAccount?> GetAccountAsync();
        Task StoreAccountAsync(AccountDetails details, AccountKey key);
    }
}
