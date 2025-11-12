using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using LetsEncryptManager.Core.Configuration;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Challenges
{
    public class AzureDnsChallengeHandler2 : IDnsChallengeHandler
    {
        private readonly ILogger<AzureDnsChallengeHandler2> logger;
        private readonly ArmClient client;

        public AzureDnsChallengeHandler2(ManagerConfig config,
            ILogger<AzureDnsChallengeHandler2> logger)
        {
            this.logger = logger;

            this.client = new ArmClient(new DefaultAzureCredential());
        }

        public async Task<ICleanableDnsRecord> HandleAsync(string type, string fullyQualifiedName, string value, KnownCertificatesConfigEntry config)
        {
            if(type != "TXT")
            {
                throw new Exception("Couldn't use provided DNS type: " + type);
            }

            logger.LogInformation("[Azure DNS 2]: Handling {0} record, {1}:{2}", type, fullyQualifiedName, value);

            var zone = await LocateZone(fullyQualifiedName);

            if(zone == null)
            {
                throw new Exception($"Could not find DNS Zone for {fullyQualifiedName} in available subscriptions");
            }

            var zoneName = zone.Data.Name;

            var relativeName = fullyQualifiedName.Replace("." + zoneName, "");

            logger.LogInformation("[Azure DNS 2]: Using relative name {0}", relativeName);


            var set = await GetOrCreateRecordSetAsync(zone, zone.Id.ResourceGroupName, zoneName, relativeName, value);
           
            while(true)
            {
                logger.LogInformation("[Azure DNS 2]: Polling record set for updated value");

                var fetched = await zone.GetDnsTxtRecordAsync(relativeName);

                if(fetched == null || fetched.Value == null)
                {
                    await Task.Delay(1000);
                    continue;
                }

                if (fetched.Value.Data.DnsTxtRecords.Any(v => v.Values.Any(vv => vv == value)))
                {
                    set = fetched.Value;
                    logger.LogInformation("[Azure DNS 2]: Updated value found, done!");
                    break;
                }
            }

            return new AzureCleanableDnsRecord2(this, zone, set, value);
        }

        private async Task<DnsZoneResource?> LocateZone(string fullyQualifiedName)
        {
            Func<DnsZoneResource, bool> selector = z => Regex.Match(fullyQualifiedName, z.Data.Name + "$", RegexOptions.IgnoreCase).Success;

            await foreach(var sub in client.GetSubscriptions().GetAllAsync())
            {
                logger.LogInformation("[Azure DNS 2]: Checking subscription {0} for DNS zone {1}", sub.Id, fullyQualifiedName);

                await foreach(var zone in sub.GetDnsZonesAsync())
                {
                    await zone.GetAsync();

                    if(selector(zone))
                    {
                        return zone;
                    }
                }
            }

            return null;
        }


        private async Task CleanRecordSet(DnsZoneResource zone, DnsTxtRecordResource record, string value)
        {
            await record.GetAsync();

            bool shouldDelete = false;
            var updated = record.Data.DnsTxtRecords.Where(r => r.Values.FirstOrDefault() != value).ToList();

            if (updated.Count == 0)
            {
                shouldDelete = true;
            }

            if (shouldDelete)
            {
                await record.DeleteAsync(WaitUntil.Completed);
            }
            else
            {
                var resp = await record.UpdateAsync(ArmDnsModelFactory.DnsTxtRecordData(record.Id, txtRecords: updated));
            }
        }

        private async Task<DnsTxtRecordResource> GetOrCreateRecordSetAsync(DnsZoneResource zone, string resourceGroup, string zoneName, string name, string value)
        {
            var records = zone.GetDnsTxtRecords();

            var exists = await records.ExistsAsync(name);

            if (exists)
            {
                return await records.GetAsync(name);
            }

            var rrec = new DnsTxtRecordInfo();
            rrec.Values.Add(value);
            var newRecs = new List<DnsTxtRecordInfo> { rrec };

            var op = await records.CreateOrUpdateAsync(WaitUntil.Completed, name, ArmDnsModelFactory.DnsTxtRecordData(DnsTxtRecordResource.CreateResourceIdentifier(zone.Data.Id.SubscriptionId, resourceGroup, zoneName, name), name, ttl: 1, txtRecords: newRecs));

            if(!op.HasCompleted || !op.HasValue)
            {
                throw new Exception("Operation to create TXT record was not successful");
            }

            return op.Value;
        }

        private class AzureCleanableDnsRecord2 : ICleanableDnsRecord
        {
            private readonly DnsZoneResource zone;
            private readonly DnsTxtRecordResource record;
            private readonly AzureDnsChallengeHandler2 client;
            private readonly string value;

            public AzureCleanableDnsRecord2(AzureDnsChallengeHandler2 client, DnsZoneResource zone, DnsTxtRecordResource record, string value)
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
