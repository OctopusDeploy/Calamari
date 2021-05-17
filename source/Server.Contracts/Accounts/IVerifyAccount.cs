using System.Threading;
using System.Threading.Tasks;

namespace Sashimi.Server.Contracts.Accounts
{
    public interface IVerifyAccount
    {
        Task Verify(AccountDetails account, CancellationToken cancellationToken);
    }
}