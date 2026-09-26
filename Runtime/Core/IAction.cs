using System.Threading;
using System.Threading.Tasks;

namespace MySys22.DialogueEngine.Core
{

    public interface IAction
    {

        void Execute(DialogueActionContext context);

        Task ExecuteAsync(DialogueActionContext context, CancellationToken cancellationToken);

        void Cancel();
    }
}
