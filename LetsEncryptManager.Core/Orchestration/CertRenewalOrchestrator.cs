using LetsEncryptManager.Core.CertificateStore;
using LetsEncryptManager.Core.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Orchestration
{
    public class CertRenewalOrchestrator
    {
        private const int CertExpirationThreshold_Days = 30;

        private readonly KnownCertificatesConfig config;
        private readonly ICertificateStore certStore;
        private readonly CertRenewer renewer;
        private readonly ILogger logger;

        public CertRenewalOrchestrator(
            KnownCertificatesConfig config,
            ICertificateStore certStore,
            CertRenewer renewer,
            ILogger<CertRenewalOrchestrator> logger)
        {
            this.config = config;
            this.certStore = certStore;
            this.renewer = renewer;
            this.logger = logger;
        }

        public async Task RenewCertificates()
        {
            var logBuilder = new StringBuilder();

            foreach (var cert in config.Certs)
            {
                if (logBuilder.Length == 0)
                {
                    logBuilder.AppendLine("Found cert configs:");
                }

                logBuilder.AppendLine($"\t{cert.Key}");
            }

            logger.LogInformation(logBuilder.ToString());

            foreach (var cert in config.Certs)
            {
                var shouldRenew = await ShouldRenew(cert.Key, cert.Value);

                if(shouldRenew)
                {
                    try
                    {
                        await renewer.RenewCertificate(cert.Key, cert.Value);
                    }
                    catch(Exception e)
                    {
                        logger.LogError(e, "Error while trying to renew '{0}'", cert.Key);
                    }
                }
            }
        }

        private async Task<bool> ShouldRenew(string cert, KnownCertificatesConfigEntry entry)
        {
            var hostnames = entry.Hostnames;
            var info = await certStore.GetCertInfo(cert, entry);

            if (info == null)
            {
                logger.LogInformation("Renwing '{0}' as no existing cert info was found", cert);
                return true;
            }

            if ((info.Expiration - DateTimeOffset.UtcNow) < TimeSpan.FromDays(CertExpirationThreshold_Days))
            {
                logger.LogInformation("Renwing '{0}' as existing cert is nearing expirtation", cert);
                return true;
            }

            var allMatchSan = hostnames.All(h => info.SubjectAlternativeNames.Contains(h, StringComparer.OrdinalIgnoreCase));
            if(allMatchSan == false)
            {
                logger.LogInformation("Renwing '{0}' as all hostnames are not contained in cert SAN", cert);
                return true;
            }

            logger.LogInformation("Skipping renew for '{0}'", cert);

            return false;
        }
    }
}
