using LetsEncryptManager.Core.Cloudflare;
using LetsEncryptManager.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Challenges
{
    public class DnsProviderFactory
    {
        private readonly IServiceProvider services;

        public DnsProviderFactory(IServiceProvider services)
        {
            this.services = services;
        }

        public IDnsChallengeHandler GetDnsProvider(string? providerName)
        {
            return providerName?.ToLower() switch
            {
                "cloudflare" => services.GetRequiredService<CloudflareDnsChallengeHandler>(),
                _ => services.GetRequiredService<AzureDnsChallengeHandler2>(),
            };
        }
    }

    public interface IDnsChallengeHandler
    {
        Task<ICleanableDnsRecord> HandleAsync(string type, string fullyQualifiedName, string value, KnownCertificatesConfigEntry config);
    }
}
