using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Cloudflare
{
    public class CloudflareHttpClient
    {
        private readonly HttpClient http;

        public CloudflareHttpClient(HttpClient http)
        {
            this.http = http;
        }

        public async Task<V4PagePaginationArray<Zone>> GetDnsZones(int page = 1)
        {
            var resp = await http.GetAsync($"https://api.cloudflare.com/client/v4/zones?page={page}");

            var body = await resp.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<V4PagePaginationArray<Zone>>(body);
        }

        public async Task<Envelope<Zone>> GetDnsZone(string zoneId)
        {
            var resp = await http.GetAsync($"https://api.cloudflare.com/client/v4/zones/{zoneId}");

            var body = await resp.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<Envelope<Zone>>(body);
        }

        public async Task<V4PagePaginationArray<RecordResponse>> GetDnsZoneRecords(string zoneId, string recordType, string name)
        {
            var resp = await http.GetAsync($"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records?type={recordType}&name.exact={UrlEncoder.Default.Encode(name)}");

            var body = await resp.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<V4PagePaginationArray<RecordResponse>>(body);
        }

        public async Task<Envelope<RecordResponse>> GetDnsZoneRecord(string zoneId, string recordId)
        {
            var resp = await http.GetAsync($"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records/{recordId}");

            var body = await resp.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<Envelope<RecordResponse>>(body);
        }

        public async Task<Envelope<Id>> DeleteDnsZoneRecord(string zoneId, string recordId)
        {
            var resp = await http.DeleteAsync($"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records/{recordId}");

            var body = await resp.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<Envelope<Id>>(body);
        }

        public async Task<Envelope<RecordResponse>> CreateDnsZoneRecord(string zoneId, string type, string name, string content, int ttl = 1, bool proxied = false)
        {
            var request = new StringContent(JsonSerializer.Serialize(new
            {
                type = type,
                name = name,
                content = content,
                ttl = ttl,
                proxied = proxied
            }), Encoding.UTF8, "application/json");

            var resp = await http.PostAsync($"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records", request);

            var body = await resp.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<Envelope<RecordResponse>>(body);
        }

        public async Task<Envelope<RecordResponse>> OverwriteDnsZoneRecord(string zoneId, string recordId, string type, string name, string content, int ttl = 1, bool proxied = false)
        {
            var request = new StringContent(JsonSerializer.Serialize(new
            {
                type = type,
                name = name,
                content = content,
                ttl = ttl,
                proxied = proxied
            }), Encoding.UTF8, "application/json");

            var resp = await http.PutAsync($"https://api.cloudflare.com/client/v4/zones/{zoneId}/dns_records/{recordId}", request);

            var body = await resp.Content.ReadAsStreamAsync();

            return await JsonSerializer.DeserializeAsync<Envelope<RecordResponse>>(body);
        }
    }
}
