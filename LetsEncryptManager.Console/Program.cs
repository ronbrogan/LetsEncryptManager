﻿using Azure.Identity;
using LetsEncryptManager.Core;
using LetsEncryptManager.Core.Account;
using LetsEncryptManager.Core.CertificateStore;
using LetsEncryptManager.Core.Challenges;
using LetsEncryptManager.Core.Configuration;
using LetsEncryptManager.Core.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LetsEncryptManager.Cli
{
    class Program
    {
        private const string AzConfigKey = "CertAzConfigUrl";

        static async Task Main(string[] args)
        {
            Exception ex = null;

            //var azConfigUrl = Environment.GetEnvironmentVariable(AzConfigKey);
            //
            //if(azConfigUrl == null)
            //{
            //    throw new Exception($"Couldn't get Azure App Configuration URL, set '{AzConfigKey}'");
            //}

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(cfg => cfg
                    // You can use appsettings.json files instead of Az AppConfig if desired
                    .AddJsonFile("local.appsettings.json")
                    //.AddAzureAppConfiguration(az =>
                    //    az.Connect(new Uri(azConfigUrl), new DefaultAzureCredential())
                    //)
                    .AddEnvironmentVariables())
                .ConfigureServices((host,svc) =>
                {
                    svc.Configure<ManagerConfig>(host.Configuration.GetSection(nameof(ManagerConfig)))
                        .AddTransient(s => s.GetService<IOptions<ManagerConfig>>().Value)
                        .Configure<KnownCertificatesConfig>(host.Configuration)
                        .AddTransient(s => s.GetService<IOptions<KnownCertificatesConfig>>().Value)
                        .AddSingleton<AzureKeyVaultStore>()
                        .AddSingleton<IAccountStore>(s => s.GetService<AzureKeyVaultStore>())
                        .AddSingleton<ICertificateStore>(s => new CompositeCertificateStore(
                            //s.GetService<AzureKeyVaultStore>(),
                            new FileSystemCertificateStore("D:\\letsencrypt")
                        ))
                        .AddSingleton<IDnsChallengeHandler, AzureDnsChallengeHandler>()
                        .AddSingleton<CertRenewer>()
                        .AddSingleton<CertRenewalOrchestrator>()
                        .AddLogging(l => l.AddConsole());

                    svc.AddHostedService<CertManagerService>();
                });

            try
            {
                using(var host = builder.Build())
                    await host.RunAsync();
            }
            catch(Exception e)
            {
                ex = e;
                Console.WriteLine(e.Message);
            }
        }
    }

    public class CertManagerService : BackgroundService
    {
        private readonly CertRenewalOrchestrator orchestrator;
        private readonly IHostApplicationLifetime host;

        public CertManagerService(CertRenewalOrchestrator orchestrator, IHostApplicationLifetime host)
        {
            this.orchestrator = orchestrator;
            this.host = host;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await orchestrator.RenewCertificates();
            host.StopApplication();
        }
    }
}
