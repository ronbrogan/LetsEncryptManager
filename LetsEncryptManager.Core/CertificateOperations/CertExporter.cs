using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.IO;

namespace LetsEncryptManager.Core.CertificateOperations
{
	public static class CertExporter
	{
		public static byte[] ExportPfx(byte[] pemCert, byte[] pemPrivateKey)
		{
			var parsedCert = new X509CertificateParser().ReadCertificate(pemCert);

			using var privateStream = new MemoryStream(pemPrivateKey);
			using var privateReader = new StreamReader(privateStream);
			var reader = new PemReader(privateReader);

			var keyPair = reader.ReadObject() as AsymmetricCipherKeyPair;
			if (keyPair == null)
			{
				throw new Exception("Could not read key pair from provided private key bytes");
			}

			var x509Name = new X509Name(parsedCert.SubjectDN.ToString());
			var alias = (x509Name.GetValueList(X509Name.CN)?[0] ?? parsedCert.SubjectDN.ToString()) as string;

			var store = new Pkcs12StoreBuilder().Build();
			store.SetKeyEntry(alias, new AsymmetricKeyEntry(keyPair.Private), new[] { new X509CertificateEntry(parsedCert) });

			using var ms = new MemoryStream();
			store.Save(ms, new char[0], new SecureRandom());
			ms.Seek(0, SeekOrigin.Begin);

			return ms.ToArray();
		}
	}
}
