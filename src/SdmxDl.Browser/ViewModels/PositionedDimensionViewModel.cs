using System.Reactive.Linq;
using LanguageExt;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.ViewModels;

public class PositionedDimensionViewModel : BaseViewModel
{
    public int CurrentPosition { get; }

    [Reactive]
    public Option<int> DesiredPosition { get; private set; }

    public int ShiftSign => DesiredPosition.Match(p => p - CurrentPosition, () => 0);

    public Dimension Dimension { get; }
    public string Name => Dimension.Name;

    public RxCommand MoveForward { get; }
    public RxCommand MoveBackward { get; }

    public PositionedDimensionViewModel(Dimension dimension, int position, int dimensionCount)
    {
        Dimension = dimension;
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
                DesiredPosition = CurrentPosition - 1;
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
                DesiredPosition = CurrentPosition + 1;
            },
            canMoveBackward
        );
    }
}
