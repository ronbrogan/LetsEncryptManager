using ACMESharp.Crypto.JOSE;
using ACMESharp.Crypto.JOSE.Impl;
using System;

namespace LetsEncryptManager.Core.Account
{
    public class AccountKey
    {
        public string KeyType { get; set; } = null!;
        public string KeyExport { get; set; } = null!;

        public IJwsTool GenerateTool()
        {
            if (KeyType.StartsWith("ES"))
            {
                var tool = new ESJwsTool();
                tool.HashSize = int.Parse(KeyType.Substring(2));
                tool.Init();
                tool.Import(KeyExport);
                return tool;
            }

            if (KeyType.StartsWith("RS"))
            {
                var tool = new RSJwsTool();
                tool.HashSize = int.Parse(KeyType.Substring(2));
                tool.Init();
                tool.Import(KeyExport);
                return tool;
            }

            throw new Exception($"Unknown or unsupported KeyType [{KeyType}]");
        }
    }
}
