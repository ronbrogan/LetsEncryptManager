using ACMESharp.Crypto.JOSE;
using ACMESharp.Protocol;

namespace LetsEncryptManager.Core.Account
{
    public class AuthorizedAccount
    {
        public AuthorizedAccount(AccountDetails account, IJwsTool signer)
        {
            this.Account = account;
            this.Signer = signer;
        }

        public AccountDetails Account { get; }
        public IJwsTool Signer { get; }
    }
}
