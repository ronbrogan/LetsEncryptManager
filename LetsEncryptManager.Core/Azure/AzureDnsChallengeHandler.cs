using Azure.Identity;
using LetsEncryptManager.Core.Configuration;
using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Challenges
{
    public class AzureDnsChallengeHandler : IDnsChallengeHandler, IDisposable
    {
        private readonly ILogger<AzureDnsChallengeHandler> logger;
        private readonly DnsManagementClient dnsClient;

        public AzureDnsChallengeHandler(ManagerConfig config,
            ILogger<AzureDnsChallengeHandler> logger)
        {
            var cred = new TokenCredentials(new AzureIdentityProvider("https://management.azure.com/"));
            this.dnsClient = new DnsManagementClient(cred);
            this.dnsClient.SubscriptionId = config.SubscriptionId;
            this.logger = logger;
        }

        public void Dispose()
        {
            this.dnsClient.Dispose();
        }

        public async Task<ICleanableDnsRecord> HandleAsync(string type, string fullyQualifiedName, string value)
        {
            if(Enum.TryParse(type, out RecordType recordType) == false)
            {
                throw new Exception("Couldn't parse provided DNS type: " + type);
            }

            logger.LogInformation("[Azure DNS]: Handling {0} record, {1}:{2}", type, fullyQualifiedName, value);

            Func<Zone, bool> selector = z => Regex.Match(fullyQualifiedName, z.Name + "$", RegexOptions.IgnoreCase).Success;

            var zoneClient = this.dnsClient.Zones;
            var zones = await zoneClient.ListAsync();

            var zone = zones.FirstOrDefault(selector);

            while (zone == null && string.IsNullOrWhiteSpace(zones.NextPageLink) == false)
            {
                zones = await zoneClient.ListNextAsync(zones.NextPageLink);

                // TODO: This doesn't support delegating subdomains to other zones
                // Fix would be to check this zone for any NS records that are more specific and follow the chain
                zone = zones.FirstOrDefault(selector);
            }

            if(zone == null)
            {
                throw new Exception($"Could not find DNS Zone for {fullyQualifiedName} in subscription {this.dnsClient.SubscriptionId}");
            }

            var relativeName = fullyQualifiedName.Replace("." + zone.Name, "");

            logger.LogInformation("[Azure DNS]: Using relative name {0}", relativeName);

            var zoneResouce = ResourceId.FromString(zone.Id);

            var set = await GetOrCreateRecordSetAsync(dnsClient.RecordSets, recordType, zoneResouce.ResourceGroupName, zone.Name, relativeName, value);
            var newSet = await dnsClient.RecordSets.CreateOrUpdateAsync(zoneResouce.ResourceGroupName, zone.Name, relativeName, recordType, set);

            while(true)
            {

                logger.LogInformation("[Azure DNS]: Polling record set for updated value");

                var fetched = await dnsClient.RecordSets.GetAsync(zoneResouce.ResourceGroupName, zone.Name, relativeName, recordType);

                if (fetched.TxtRecords.Any(t => t.Value.Any(v => v == value)))
                {
                    logger.LogInformation("[Azure DNS]: Updated value found, done!");
                    break;
                }
            }

            return new AzureCleanableDnsRecord(this, zoneResouce.ResourceGroupName, zone.Name, newSet, recordType, value);
        }

        private async Task CleanRecordSet(string resourceGroup, string zone, RecordSet set, RecordType type, string value)
        {
            bool shouldDelete = false;

            switch (type)
            {
                case RecordType.TXT:
                    set.TxtRecords = set.TxtRecords.Where(t => t.Value.FirstOrDefault() != value).ToArray();
                    shouldDelete = (set.TxtRecords.Count == 0);
                    break;

                default:
                    throw new Exception("Record type is unsupported: " + type.ToString());
            }

            if (shouldDelete)
            {
                await this.dnsClient.RecordSets.DeleteAsync(resourceGroup, zone, set.Name, type);
            }
            else
            {
                await this.dnsClient.RecordSets.UpdateAsync(resourceGroup, zone, set.Name, type, set);
            }
        }

        private async Task<RecordSet> GetOrCreateRecordSetAsync(IRecordSetsOperations recordSetClient, RecordType type, string resourceGroup, string zoneName, string name, string value)
        {
            RecordSet set = null!;

            try
            {
                set = await recordSetClient.GetAsync(resourceGroup, zoneName, name, type);
            }
            catch (CloudException e)
            {
                if (e.Response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            if(set == null)
            {
                set = new RecordSet(name: name, tTL: 1);
                switch (type)
                {
                    case RecordType.TXT:
                        set.TxtRecords = new List<TxtRecord>();
                        break;

                    default:
                        throw new Exception("Record type is unsupported: " + type.ToString());
                }
            }

            
            switch (type)
            {
                case RecordType.TXT:
                    set.TxtRecords.Add(new TxtRecord(new[] { value }));
                    break;

                default:
                    throw new Exception("Record type is unsupported: " + type.ToString());
            }

            return set;
        }

        private class AzureCleanableDnsRecord : ICleanableDnsRecord
        {
            private readonly string resourceGroup;
            private readonly string zone;
            private readonly AzureDnsChallengeHandler client;
            private readonly RecordSet set;
            private readonly RecordType type;
            private readonly string value;

            public AzureCleanableDnsRecord(AzureDnsChallengeHandler client, string resourceGroup, string zone, RecordSet set, RecordType type, string value)
            {
                this.resourceGroup = resourceGroup;
                this.zone = zone;
                this.client = client;
                this.set = set;
                this.type = type;
                this.value = value;
            }

            public Task CleanAsync()
            {
                return client.CleanRecordSet(resourceGroup, zone, set, type, value);
            }
        }

        private class AzureIdentityProvider : ITokenProvider
        {
            private readonly string scope;
            private DefaultAzureCredential cred;

            public AzureIdentityProvider(string scope)
            {
                this.cred = new DefaultAzureCredential();
                this.scope = scope;
            }

            public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
            {
                var token = await cred.GetTokenAsync(new Azure.Core.TokenRequestContext(new[] { this.scope }));

                return new AuthenticationHeaderValue("Bearer", token.Token);
            }
        }
    }
}
