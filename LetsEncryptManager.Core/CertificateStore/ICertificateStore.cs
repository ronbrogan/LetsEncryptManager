using System.Threading.Tasks;

namespace LetsEncryptManager.Core.CertificateStore
{
    public interface ICertificateStore
    {
        Task StorePfxCertificateAsync(string identifier, byte[] pfx);
        Task<CertInfo?> GetCertInfo(string identifier);
    }
}