using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Challenges
{
    public interface IDnsChallengeHandler
    {
        Task<ICleanableDnsRecord> HandleAsync(string type, string fullyQualifiedName, string value);
    }
}
