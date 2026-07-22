using System.Threading;

namespace Aptema.App.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    internal const string PrimaryMutexName = "Local\\Aptema.Desktop.SingleInstance";
    internal const string PrimaryEventName = "Local\\Aptema.Desktop.ShowWindow";
    internal const string LegacyMutexName = "Local\\LightPilot.Desktop.SingleInstance";
    internal const string LegacyEventName = "Local\\LightPilot.Desktop.ShowWindow";

    private readonly Mutex[] _mutexes;
    private readonly EventWaitHandle[] _activationEvents;
    private CancellationTokenSource? _listenerCts;

    private SingleInstanceGuard(Mutex[] mutexes, EventWaitHandle[] activationEvents)
    {
        _mutexes = mutexes;
        _activationEvents = activationEvents;
    }

    public static bool TryAcquire(out SingleInstanceGuard? guard)
    {
        var primary = new Mutex(initiallyOwned: true, PrimaryMutexName, out var primaryCreated);
        if (!primaryCreated)
        {
            Signal(PrimaryEventName);
            primary.Dispose();
            guard = null;
            return false;
        }

        var legacy = new Mutex(initiallyOwned: true, LegacyMutexName, out var legacyCreated);
        if (!legacyCreated)
        {
            Signal(LegacyEventName);
            legacy.Dispose();
            primary.ReleaseMutex();
            primary.Dispose();
            guard = null;
            return false;
        }

        guard = new SingleInstanceGuard(
            [primary, legacy],
            [
                new EventWaitHandle(false, EventResetMode.AutoReset, PrimaryEventName),
                new EventWaitHandle(false, EventResetMode.AutoReset, LegacyEventName)
            ]);
        return true;
    }

    public void StartActivationListener(Action activate)
    {
        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (WaitHandle.WaitAny(_activationEvents, TimeSpan.FromMilliseconds(500)) != WaitHandle.WaitTimeout) activate();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        foreach (var activationEvent in _activationEvents) activationEvent.Dispose();
        foreach (var mutex in _mutexes)
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    private static void Signal(string eventName)
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(eventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }
}
