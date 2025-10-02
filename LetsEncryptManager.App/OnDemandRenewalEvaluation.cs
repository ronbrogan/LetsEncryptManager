using LetsEncryptManager.Core.Orchestration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LetsEncryptManager.App
{
    public class OnDemandRenewalEvaluation
    {
        private readonly CertRenewalOrchestrator renewalOrchestrator;
        private readonly ILogger log;

        public OnDemandRenewalEvaluation(CertRenewalOrchestrator renewalOrchestrator, ILogger<OnDemandRenewalEvaluation> log)
        {
            this.renewalOrchestrator = renewalOrchestrator;
            this.log = log;
        }

        [Function("OnDemandRenewalEvaluation")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            log.LogInformation("Kicking off cert renewal task...");

            await this.renewalOrchestrator.RenewCertificates();

            return new OkResult();
        }
    }
}
