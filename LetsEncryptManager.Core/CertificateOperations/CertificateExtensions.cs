using System.Collections.Generic;
using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace LetsEncryptManager.Core.CertificateOperations
{
    public static class CertificateExtensions
    {
        public const string SubjectAlternativeNameOid = "2.5.29.17";
        private const int DnsNameTag = 2;

        // AsnEncodedData.Format() renders SANs using the OS-native crypto formatter, whose output
        // differs between Windows ("DNS Name=host") and Linux ("DNS:host") - parsing the DER
        // GeneralNames sequence directly avoids depending on that platform-specific string format.
        public static string[] GetSubjectAlternativeNames(this X509Certificate2 cert)
        {
            var names = new List<string>();

            foreach (var e in cert.Extensions)
            {
                if (e.Oid?.Value != SubjectAlternativeNameOid)
                    continue;

                var reader = new AsnReader(e.RawData, AsnEncodingRules.DER);
                var generalNames = reader.ReadSequence();

                while (generalNames.HasData)
                {
                    var tag = generalNames.PeekTag();

                    if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == DnsNameTag)
                    {
                        names.Add(generalNames.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, DnsNameTag)));
                    }
                    else
                    {
                        generalNames.ReadEncodedValue();
                    }
                }
            }

            return names.ToArray();
        }
    }
}
