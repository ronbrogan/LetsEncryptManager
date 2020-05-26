using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.CertificateStore
{
    public class FileSystemCertificateStore : ICertificateStore
    {
        private readonly string root;

        public FileSystemCertificateStore(string root)
        {
            this.root = root;
            Directory.CreateDirectory(root);
        }

        public async Task<CertInfo?> GetCertInfo(string identifier)
        {
            var path = Path.Combine(this.root, identifier + ".pfx");

            if(File.Exists(path) == false)
            {
                return null;
            }

            using var oldCert = X509Certificate.CreateFromCertFile(path);
            using var cert = new X509Certificate2(oldCert);

            return new CertInfo(identifier, cert);
        }

        public Task StorePfxCertificateAsync(string identifier, byte[] pfx)
        {
            var path = Path.Combine(this.root, identifier + ".pfx");

            File.WriteAllBytes(path, pfx);

            return Task.CompletedTask;
        }
    }
}
