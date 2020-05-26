using LetsEncryptManager.Core.CertificateOperations;
using System;
using System.Security.Cryptography.X509Certificates;

namespace LetsEncryptManager.Core.CertificateStore
{
    public class CertInfo
    {
        public string Identifier { get; }
        public DateTimeOffset Expiration { get; }
        public string SubjectName { get; set; }
        public string SubjectAlternativeNames { get; }

        public CertInfo(string identifier, X509Certificate2 cert)
        {
            this.Identifier = identifier;
            this.Expiration = cert.NotAfter;
            this.SubjectName = cert.Subject;
            this.SubjectAlternativeNames = cert.GetSubjectAlternativeNames();
        }
    }
}
