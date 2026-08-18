using LetsEncryptManager.Core.Configuration;
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

        public Task<CertInfo?> GetCertInfo(string identifier, KnownCertificatesConfigEntry config)
        {
            return stores.First().GetCertInfo(identifier, config);
        }

        public Task StorePfxCertificateAsync(string identifier, byte[] pfx, KnownCertificatesConfigEntry config)
        {
            return Task.WhenAll(stores.Select(s => s.StorePfxCertificateAsync(identifier, pfx, config)));
        }
    }
}
