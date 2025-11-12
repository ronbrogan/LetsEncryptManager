using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace LetsEncryptManager.Core.Configuration
{
    public class KnownCertificatesConfig
    {
        /// <summary>
        /// Mapping of Cert name to hostnames
        /// </summary>
        public Dictionary<string, KnownCertificatesConfigEntry> Certs { get; set; } = null!;

        public static KnownCertificatesConfig Bind(IConfiguration config)
        {
            var knownCertsConfig = new KnownCertificatesConfig() { Certs = new() };

            var section = config.GetSection("Certs");

            foreach(var cert in section.GetChildren())
            {
                var entry = new KnownCertificatesConfigEntry()
                {
                    CertName = cert.Key,
                    Hostnames = cert.Value?.Split(',') ?? []
                };

                foreach(var opts in cert.GetChildren())
                {
                    if(opts.Key == "DnsSubscription")
                    {
                        entry.DnsSubscription = opts.Value;
                    }
                    else if(opts.Key == "DnsProvider")
                    {
                        entry.DnsProvider = opts.Value;
                    }
                    else if(opts.Key == "KeyVaultUrl")
                    {
                        entry.KeyVaultUrl = opts.Value;
                    }
                }

                knownCertsConfig.Certs[cert.Key] = entry;
            }

            return knownCertsConfig;
        }
    }

    public class KnownCertificatesConfigEntry
    {
        public string CertName { get; set; } = null!;
        public string[] Hostnames { get; set; } = null!;
        public string? DnsSubscription { get; set; }
        public string? DnsProvider { get; set; }
        public string? KeyVaultUrl { get; set; }
    }
}

