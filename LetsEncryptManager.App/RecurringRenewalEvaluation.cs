using LetsEncryptManager.Core.Orchestration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace LetsEncryptManager.App
{
    public class RecurringRenewalEvaluation
    {
        private readonly CertRenewalOrchestrator renewalOrchestrator;
        private readonly ILogger log;

        public RecurringRenewalEvaluation(CertRenewalOrchestrator renewalOrchestrator, ILogger<RecurringRenewalEvaluation> log)
        {
            this.renewalOrchestrator = renewalOrchestrator;
            this.log = log;
        }

        [Function("RecurringRenewalEvaluation")]
        public async Task Run([TimerTrigger("0 0 * * * *")]TimerInfo myTimer)
        {
            log.LogInformation("Kicking off cert renewal task...");

            await this.renewalOrchestrator.RenewCertificates();
        }
    }
}
