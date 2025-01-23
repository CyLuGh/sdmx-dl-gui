using System.Threading;
using System.Threading.Tasks;

namespace SdmxDl.Browser.ViewModels;

public abstract class CancellableBaseViewModel : BaseViewModel
{
    protected CancellationTokenSource CancelTokenSource { get; private set; } = new();

    internal async Task Reset()
    {
        await CancelTokenSource.CancelAsync().ConfigureAwait(false);
        CancelTokenSource.Dispose();
        CancelTokenSource = new();
    }
}
