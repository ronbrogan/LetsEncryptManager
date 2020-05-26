using LetsEncryptManager.Core.CertificateStore;
using LetsEncryptManager.Core.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Orchestration
{
    public class CertRenewalOrchestrator
    {
        private const int CertExpirationThreshold_Days = 10;

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
                var hostnames = cert.Value.Split(',');

                var shouldRenew = await ShouldRenew(cert.Key, hostnames);

                if(shouldRenew)
                {
                    try
                    {
                        await renewer.RenewCertificate(cert.Key, hostnames);
                    }
                    catch(Exception e)
                    {
                        logger.LogError(e, "Error while trying to renew '{0}'", cert.Key);
                        throw;
                    }
                }
            }
        }

        private async Task<bool> ShouldRenew(string cert, string[] hostnames)
        {
            var info = await certStore.GetCertInfo(cert);

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

            if(NameMatchesHost(hostnames.First(), info.SubjectName) == false)
            {
                logger.LogInformation("Renwing '{0}' as existing cert subject name does not match first host of '{1}'", cert, hostnames.First());
                return true;
            }

            var allMatchSan = hostnames.All(h => NameMatchesHost(h, info.SubjectAlternativeNames));
            if(allMatchSan == false)
            {
                logger.LogInformation("Renwing '{0}' as all hostnames are not contained in cert SAN", cert);
                return true;
            }

            return false;
        }

        private bool NameMatchesHost(string host, string name)
        {
            var pattern = new Regex("=" + Regex.Escape(host), RegexOptions.IgnoreCase);

            return pattern.IsMatch(name);
        }
    }
}
