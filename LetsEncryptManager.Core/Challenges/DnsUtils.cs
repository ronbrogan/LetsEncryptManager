using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;

namespace LetsEncryptManager.Core.Challenges
{
    public class DnsUtils
    { 
        public static async Task WaitUntilTxtRecord(string recordHostname, string expectedRecordValue, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[DnsUtils] Ensuring TXT record {recordHostname} of value {expectedRecordValue} exists on authoritative nameservers");

            var client = new LookupClient();

            // assume first segment is just the record name, this surely won't work for multiple subdomains but meh
            var tld = recordHostname.Substring(recordHostname.IndexOf('.') + 1);

            var nsResult = await client.QueryAsync(tld, QueryType.NS, QueryClass.IN, cancellationToken);
            var nameservers = nsResult.Answers.OfType<NsRecord>();

            Console.WriteLine($"[DnsUtils] Found Authoritative Nameservers: {string.Join(", ", nameservers.Select(ns => ns.NSDName.Value))}");

            foreach (var answer in nameservers)
            {
                var result = await client.GetHostEntryAsync(answer.NSDName.Value);

                var authoritativeClient = new LookupClient(result.AddressList);

                while(true)
                {
                    var txts = await authoritativeClient.QueryAsync($"{recordHostname}", QueryType.TXT, QueryClass.IN, cancellationToken);

                    // find matching record from txts
                    var record = txts.Answers.OfType<TxtRecord>().SelectMany(t => t.Text).FirstOrDefault(tv => tv == expectedRecordValue);

                    if (record == expectedRecordValue)
                    {
                        Console.WriteLine($"[DnsUtils] Found {recordHostname}={record} from {answer.NSDName.Value}");
                        break;
                    }

                    Console.WriteLine($"[DnsUtils] Record not found from {answer.NSDName.Value}, retrying in 10 seconds...");
                    await Task.Delay(10000);
                }
            }
        }
    }
}
