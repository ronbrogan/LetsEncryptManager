using Azure.Identity;
using LetsEncryptManager.Core;
using LetsEncryptManager.Core.Account;
using LetsEncryptManager.Core.CertificateStore;
using LetsEncryptManager.Core.Challenges;
using LetsEncryptManager.Core.Configuration;
using LetsEncryptManager.Core.Orchestration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;

namespace LetsEncryptManager.App;

public static class Program
{
    private const string AzConfigKey = "AzureConfigUrl";

    public static void Main(string[] args)
    {
        var azConfigUrl = Environment.GetEnvironmentVariable(AzConfigKey);

        if (AzConfigKey == null)
        {
            throw new Exception($"Can't startup without Azure Config URL, set '{AzConfigKey}' environment variable");
        }

        var config = new ConfigurationBuilder()
            .AddAzureAppConfiguration(az =>
                    az.Connect(new Uri(azConfigUrl), new DefaultAzureCredential())
                )
            .AddEnvironmentVariables()
            .Build();

        var builder = FunctionsApplication.CreateBuilder(args);

        builder.ConfigureFunctionsWebApplication();

        builder.Services
            .AddHttpClient()
            .AddApplicationInsightsTelemetryWorkerService()
            .ConfigureFunctionsApplicationInsights()
            .Configure<ManagerConfig>(config.GetSection(nameof(ManagerConfig)))
            .AddTransient(s => s.GetService<IOptions<ManagerConfig>>().Value)
            .AddSingleton(KnownCertificatesConfig.Bind(config))
            .AddSingleton<AzureKeyVaultStore>()
            .AddSingleton<IAccountStore>(s => s.GetService<AzureKeyVaultStore>())
            .AddSingleton<ICertificateStore>(s => s.GetService<AzureKeyVaultStore>())
            .AddSingleton<IDnsChallengeHandler, AzureDnsChallengeHandler2>()
            .AddSingleton<CertRenewer>()
            .AddSingleton<CertRenewalOrchestrator>();

        builder.Build().Run();
    }
}





