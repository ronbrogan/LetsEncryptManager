using Azure.Identity;
using LetsEncryptManager.App;
using LetsEncryptManager.Core;
using LetsEncryptManager.Core.Account;
using LetsEncryptManager.Core.CertificateStore;
using LetsEncryptManager.Core.Challenges;
using LetsEncryptManager.Core.Configuration;
using LetsEncryptManager.Core.Orchestration;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

[assembly: FunctionsStartup(typeof(Startup))]
namespace LetsEncryptManager.App
{
    public class Startup : FunctionsStartup
    {
        private const string AzConfigKey = "AzureConfigUrl";

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var azConfigUrl = Environment.GetEnvironmentVariable(AzConfigKey);

            if(AzConfigKey == null)
            {
                throw new Exception($"Can't startup without Azure Config URL, set '{AzConfigKey}' environment variable");
            }

            var config = new ConfigurationBuilder()
                .AddAzureAppConfiguration(az =>
                        az.Connect(new Uri(azConfigUrl), new DefaultAzureCredential())
                    )
                .AddEnvironmentVariables()
                .Build();

            builder.Services.Configure<ManagerConfig>(config.GetSection(nameof(ManagerConfig)))
                .AddTransient(s => s.GetService<IOptions<ManagerConfig>>().Value)
                .Configure<KnownCertificatesConfig>(config)
                .AddTransient(s => s.GetService<IOptions<KnownCertificatesConfig>>().Value)
                .AddSingleton<AzureKeyVaultStore>()
                .AddSingleton<IAccountStore>(s => s.GetService<AzureKeyVaultStore>())
                .AddSingleton<ICertificateStore>(s => s.GetService<AzureKeyVaultStore>())
                .AddSingleton<IDnsChallengeHandler, AzureDnsChallengeHandler2>()
                .AddSingleton<CertRenewer>()
                .AddSingleton<CertRenewalOrchestrator>();
        }
    }
}
