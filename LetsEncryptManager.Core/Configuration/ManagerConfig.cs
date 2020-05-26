using System.Runtime.Serialization;

namespace LetsEncryptManager.Core.Configuration
{
    public class ManagerConfig
    {
        public string? CertContactEmail { get; set; }
        public string? CertificateAuthorityUrl { get; set; }
        public string? SubscriptionId { get; set; }
        public string? KeyVaultUrl { get; set; }
    }
}
