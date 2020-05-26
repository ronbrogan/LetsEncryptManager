using System.Linq;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.CertificateStore
{
    public class CompositeCertificateStore : ICertificateStore
    {
        private readonly ICertificateStore[] stores;

        public CompositeCertificateStore(params ICertificateStore[] stores)
        {
            this.stores = stores;
        }

        public Task<CertInfo?> GetCertInfo(string identifier)
        {
            return stores.First().GetCertInfo(identifier);
        }

        public Task StorePfxCertificateAsync(string identifier, byte[] pfx)
        {
            return Task.WhenAll(stores.Select(s => s.StorePfxCertificateAsync(identifier, pfx)));
        }
    }
}
