using LetsEncryptManager.Core.Orchestration;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LetsEncryptManager.App
{
    public class RecurringRenewalEvaluation
    {
        private readonly CertRenewalOrchestrator renewalOrchestrator;

        public RecurringRenewalEvaluation(CertRenewalOrchestrator renewalOrchestrator)
        {
            this.renewalOrchestrator = renewalOrchestrator;
        }

        [FunctionName("RecurringRenewalEvaluation")]
        public async Task Run([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation("Kicking off cert renewal task...");

            await this.renewalOrchestrator.RenewCertificates();
        }
    }
}
