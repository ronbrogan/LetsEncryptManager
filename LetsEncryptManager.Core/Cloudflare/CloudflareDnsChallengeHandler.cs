
using LetsEncryptManager.Core.Challenges;
using LetsEncryptManager.Core.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Cloudflare
{
    public class CloudflareDnsChallengeHandler : IDnsChallengeHandler
    {
        private readonly ManagerConfig config;
        private readonly CloudflareHttpClient cf;
        private readonly ILogger<CloudflareDnsChallengeHandler> logger;

        public CloudflareDnsChallengeHandler(ManagerConfig config, CloudflareHttpClient cf, ILogger<CloudflareDnsChallengeHandler> logger)
        {
            this.config = config;
            this.cf = cf;
            this.logger = logger;
        }

        public async Task<ICleanableDnsRecord> HandleAsync(string type, string fullyQualifiedName, string value, KnownCertificatesConfigEntry config)
        {
            if (type != "TXT")
            {
                throw new Exception("Couldn't use provided DNS type: " + type);
            }

            logger.LogInformation("[Cloudflare DNS]: Handling {0} record, {1}:{2}", type, fullyQualifiedName, value);

            var zone = await LocateZone(fullyQualifiedName);

            if (zone == null)
            {
                throw new Exception($"Could not find DNS Zone for {fullyQualifiedName} in available subscriptions");
            }

            var zoneName = zone.name;

            var relativeName = fullyQualifiedName.Replace("." + zoneName, "");

            logger.LogInformation("[Cloudflare DNS]: Using relative name {0}", relativeName);


            var set = await GetOrCreateRecordSetAsync(zone, relativeName, value);

            while (true)
            {
                logger.LogInformation("[Cloudflare DNS]: Polling record set for updated value");

                var fetched = await cf.GetDnsZoneRecord(zone.id, set.id);

                if (!fetched.success || !fetched.result.content.Contains(value))
                {
                    await Task.Delay(1000);
                    continue;
                }

                if (fetched.result.content.Contains(value))
                {
                    set = fetched.result;
                    logger.LogInformation("[Cloudflare DNS]: Updated value found, done!");
                    break;
                }
            }

            return new CloudflareCleanableDnsRecord(this, zone, set, value);
        }

        private async Task<Zone?> LocateZone(string fullyQualifiedName)
        {
            Func<string, bool> selector = z => Regex.Match(fullyQualifiedName, z + "$", RegexOptions.IgnoreCase).Success;

            var page = 1;

            while(page > 0)
            {
                var results = await cf.GetDnsZones(page);

                if(!results.success)
                {
                    logger.LogError("[Cloudflare DNS]: Request failed for DNS zone {0}, {1}", fullyQualifiedName, string.Join("\r\n", results.errors.Select(e => e.message)));
                    return null;
                }

                foreach(var result in results.result)
                {
                    logger.LogInformation("[Cloudflare DNS]: Checking zone {0} ({1}) for DNS zone {2}", result.name, result.id, fullyQualifiedName);

                    if (selector(result.name))
                    {
                        return result;
                    }
                }

                if(results.result_info.total_pages > page)
                {
                    page++;
                }
                else
                {
                    page = 0;
                }
            }

            return null;
        }


        private async Task CleanRecordSet(Zone zone, RecordResponse record, string value)
        {
            var resp = await cf.DeleteDnsZoneRecord(zone.id, record.id);
        }

        private async Task<RecordResponse> GetOrCreateRecordSetAsync(Zone zone, string name, string value)
        {
            var records = await cf.GetDnsZoneRecords(zone.id, RecordType.TXT, name);

            RecordResponse? existingRecord = null;

            foreach(var record in records.result)
            {
                if(record.name != name)
                {
                    continue;
                }

                if (record.content == value)
                {
                    logger.LogInformation("[Cloudflare DNS]: Found existing record set for {0} with value", name);
                    return record;
                }

                existingRecord = record;
            }

            var resp = await cf.CreateDnsZoneRecord(zone.id, RecordType.TXT, name, value);

            if(!resp.success)
            {
                logger.LogError("[Cloudflare DNS]: Request failed to create TXT record {0}:{1}\r\n{2}", name, value, string.Join("\r\n", resp.errors.Select(e => e.message)));
            }

            return resp.result;
        }

        private class CloudflareCleanableDnsRecord : ICleanableDnsRecord
        {

            private readonly CloudflareDnsChallengeHandler client;
            private readonly Zone zone;
            private readonly RecordResponse record;
            private readonly string value;

            public CloudflareCleanableDnsRecord(CloudflareDnsChallengeHandler client, Zone zone, RecordResponse record, string value)
            {
                this.zone = zone;
                this.record = record;
                this.client = client;
                this.value = value;
            }

            public Task CleanAsync()
            {
                return client.CleanRecordSet(zone, record, value);
            }
        }
    }
}
