using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.ViewModels;

public class PositionedDimensionViewModel : BaseViewModel
{
    [Reactive]
    public int CurrentPosition { get; set; }

    public string Name { get; }

    public RxCommand MoveForward { get; }
    public RxCommand MoveBackward { get; }

    public PositionedDimensionViewModel(Dimension dimension, int position, int dimensionCount)
    {
        Name = dimension.Name;
        CurrentPosition = position;
        MoveForward = CreateMoveForwardCommand();
        MoveBackward = CreateMoveBackwardCommand(dimensionCount);
    }

    private RxCommand CreateMoveForwardCommand()
    {
        var canMoveForward = this.WhenAnyValue(x => x.CurrentPosition)
            .Select(pos => pos > 0)
            .ObserveOn(RxApp.MainThreadScheduler);
        return ReactiveCommand.Create(
            () =>
            {
                CurrentPosition--;
            },
            canMoveForward
        );
    }

    private RxCommand CreateMoveBackwardCommand(int count)
    {
        var canMoveBackward = this.WhenAnyValue(x => x.CurrentPosition)
            .Select(pos => pos < count - 1)
            .ObserveOn(RxApp.MainThreadScheduler);
        return ReactiveCommand.Create(
            () =>
            {
                CurrentPosition++;
            },
            canMoveBackward
        );
    }
}
