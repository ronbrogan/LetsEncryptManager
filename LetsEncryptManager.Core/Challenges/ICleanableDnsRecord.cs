using System.Threading.Tasks;

namespace LetsEncryptManager.Core.Challenges
{
    public interface ICleanableDnsRecord
    {
        Task CleanAsync();
    }
}
