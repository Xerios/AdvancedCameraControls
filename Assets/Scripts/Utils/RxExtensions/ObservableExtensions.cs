
using System;
using System.Reactive.Disposables;

public static class ObservableExtensions
{
    // Update is called once per frame
    public static void AddTo(this IDisposable observable, CompositeDisposable composite)
    {
        composite.Add(observable);
    }
}
