namespace LetsEncryptManager.Core
{
    public class CertRenewerConfig
    {
        public string ContactEmail { get; set; }
        public string CertAuthorityUrl { get; set; } = "https://acme-staging-v02.api.letsencrypt.org";

        public CertRenewerConfig(string contactEmail, string? certAuthorityUrl = null)
        {
            this.ContactEmail = contactEmail;

            if(certAuthorityUrl != null)
            {
                CertAuthorityUrl = certAuthorityUrl;
            }
        }
    }
}
