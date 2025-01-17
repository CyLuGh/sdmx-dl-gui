using System;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Subjects;

namespace SdmxDl.Browser.Infrastructure;

public class ExceptionHandler : IObserver<Exception>
{
    private readonly Subject<Exception> _alerts = new();
    public IObservable<Exception> Alerts => _alerts;

    public void OnCompleted()
    {
        if (Debugger.IsAttached)
            Debugger.Break();
    }

    public void OnError(Exception error)
    {
        if (Debugger.IsAttached)
            Debugger.Break();
    }

    public void OnNext(Exception value)
    {
        if (Debugger.IsAttached)
            Debugger.Break();

        _alerts.OnNext(value);
    }
}
