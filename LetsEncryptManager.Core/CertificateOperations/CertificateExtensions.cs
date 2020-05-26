using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LetsEncryptManager.Core.CertificateOperations
{
	public static class CertificateExtensions
    {
        public const string SubjectAlternativeNameOid = "2.5.29.17";

		public static string GetSubjectAlternativeNames(this X509Certificate2 cert)
		{
			foreach (var e in cert.Extensions)
			{
				if (e.Oid.Value != SubjectAlternativeNameOid)
					continue;

				var asn = new AsnEncodedData(e.Oid, e.RawData);
				return asn.Format(false);
			}

			return "";
		}
	}
}
