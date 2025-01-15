using ReactiveUI;

namespace SdmxDl.Browser.ViewModels;

public abstract class BaseViewModel : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
}
