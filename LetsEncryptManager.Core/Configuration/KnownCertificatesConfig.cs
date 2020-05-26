using System.Collections.Generic;

namespace LetsEncryptManager.Core.Configuration
{
    public class KnownCertificatesConfig
    {
        /// <summary>
        /// Mapping of Cert name to hostnames
        /// </summary>
        public Dictionary<string, string> Certs { get; set; } = null!;
    }
}
