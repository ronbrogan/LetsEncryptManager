using LetsEncryptManager.Core.Configuration;
using System;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Challenges
{
    public class NullDnsChallengeHandler : IDnsChallengeHandler
    {
        public Task<ICleanableDnsRecord> HandleAsync(string type, string name, string value, KnownCertificatesConfigEntry config)
        {
            Console.WriteLine($"Create [{type}] {name}:{value}");
            return Task.FromResult((ICleanableDnsRecord)new NullCleanableRecord(type, name, value));
        }

        private class NullCleanableRecord : ICleanableDnsRecord
        {
            private readonly string type;
            private readonly string name;
            private readonly string value;

            public NullCleanableRecord(string type, string name, string value)
            {
                this.type = type;
                this.name = name;
                this.value = value;
            }

            public Task CleanAsync()
            {
                Console.WriteLine($"Cleaned up record: [{type}] {name}:{value}");
                return Task.CompletedTask;
            }
        }
    }
}
