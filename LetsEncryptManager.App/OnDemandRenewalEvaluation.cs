using LetsEncryptManager.Core.Orchestration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LetsEncryptManager.App
{
    public class OnDemandRenewalEvaluation
    {
        private readonly CertRenewalOrchestrator renewalOrchestrator;

        public OnDemandRenewalEvaluation(CertRenewalOrchestrator renewalOrchestrator)
        {
            this.renewalOrchestrator = renewalOrchestrator;
        }

        [FunctionName("OnDemandRenewalEvaluation")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Kicking off cert renewal task...");

            await this.renewalOrchestrator.RenewCertificates();

            return new OkResult();
        }
    }
}
