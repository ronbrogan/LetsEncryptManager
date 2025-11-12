using System;
using System.Collections.Generic;
using System.Text;

namespace LetsEncryptManager.Core.Cloudflare
{

    public class V4PagePaginationArray<T>
    {
        public ResponseInfo[] errors { get; set; }
        public ResponseInfo[] messages { get; set; }
        public bool success { get; set; }
        public T[] result { get; set; }
        public Result_Info result_info { get; set; }
    }

    public class Envelope<T>
    {
        public ResponseInfo[] errors { get; set; }
        public ResponseInfo[] messages { get; set; }
        public bool success { get; set; }
        public T result { get; set; }

    }

    public class Result_Info
    {
        public int count { get; set; }
        public int page { get; set; }
        public int per_page { get; set; }
        public int total_count { get; set; }
        public int total_pages { get; set; }
    }

    public class ResponseInfo
    {
        public int code { get; set; }
        public string message { get; set; }
        public string documentation_url { get; set; }
        public ResponseInfoSource source { get; set; }
    }

    public class ResponseInfoSource
    {
        public string pointer { get; set; }
    }


    public class Zone
    {
        public string id { get; set; }
        public Account account { get; set; }
        public DateTime activated_on { get; set; }
        public DateTime created_on { get; set; }
        public int development_mode { get; set; }
        public Meta meta { get; set; }
        public DateTime modified_on { get; set; }
        public string name { get; set; }
        public string[] name_servers { get; set; }
        public string original_dnshost { get; set; }
        public string[] original_name_servers { get; set; }
        public string original_registrar { get; set; }
        public Owner owner { get; set; }
        public Plan plan { get; set; }
        public string cname_suffix { get; set; }
        public bool paused { get; set; }
        public string[] permissions { get; set; }
        public string status { get; set; }
        public Tenant tenant { get; set; }
        public Tenant_Unit tenant_unit { get; set; }
        public string type { get; set; }
        public string[] vanity_name_servers { get; set; }
        public string verification_key { get; set; }
    }

    public class Account
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class Meta
    {
        public bool cdn_only { get; set; }
        public int custom_certificate_quota { get; set; }
        public bool dns_only { get; set; }
        public bool foundation_dns { get; set; }
        public int page_rule_quota { get; set; }
        public bool phishing_detected { get; set; }
        public int step { get; set; }
    }

    public class Owner
    {
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
    }

    public class Plan
    {
        public string id { get; set; }
        public bool can_subscribe { get; set; }
        public string currency { get; set; }
        public bool externally_managed { get; set; }
        public string frequency { get; set; }
        public bool is_subscribed { get; set; }
        public bool legacy_discount { get; set; }
        public string legacy_id { get; set; }
        public string name { get; set; }
        public float price { get; set; }
    }

    public class Tenant
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class Tenant_Unit
    {
        public string id { get; set; }
    }


    public class RecordType
    {
        public const string A = "A";
        public const string AAAA = "AAAA";
        public const string CAA = "CAA";
        public const string CERT = "CERT";
        public const string CNAME = "CNAME";
        public const string DNSKEY = "DNSKEY";
        public const string DS = "DS";
        public const string HTTPS = "HTTPS";
        public const string LOC = "LOC";
        public const string MX = "MX";
        public const string NAPTR = "NAPTR";
        public const string NS = "NS";
        public const string OPENPGPKEY = "OPENPGPKEY";
        public const string PTR = "PTR";
        public const string SMIMEA = "SMIMEA";
        public const string SRV = "SRV";
        public const string SSHFP = "SSHFP";
        public const string SVCB = "SVCB";
        public const string TLSA = "TLSA";
        public const string TXT = "TXT";
        public const string URI = "URI";
    }

    public class RecordResponse
    {

        public string name { get; set; }
        public int ttl { get; set; }
        public string type { get; set; }
        public string comment { get; set; }
        public string content { get; set; }
        public bool proxied { get; set; }
        public RecordSettings settings { get; set; }
        public string[] tags { get; set; }
        public string id { get; set; }
        public DateTime created_on { get; set; }
        public Meta meta { get; set; }
        public DateTime modified_on { get; set; }
        public bool proxiable { get; set; }
        public DateTime comment_modified_on { get; set; }
        public DateTime tags_modified_on { get; set; }
    }

    public class RecordSettings
    {
        public bool ipv4_only { get; set; }
        public bool ipv6_only { get; set; }
    }

    public class Id
    {
        public string id { get; set; }
    }
}
