using LetsEncryptManager.Core.Configuration;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.CertificateStore
{
    public interface ICertificateStore
    {
        Task StorePfxCertificateAsync(string identifier, byte[] pfx, KnownCertificatesConfigEntry config);
        Task<CertInfo?> GetCertInfo(string identifier, KnownCertificatesConfigEntry config);
    }
}