using System.Runtime.InteropServices;

namespace SteamVrAddonManager;

/// <summary>
/// Ensures only one GUI instance runs. A second launch signals the first to come to the front and
/// exits. Headless (<c>--apply</c>) runs bypass this entirely.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = "SteamVrAddonManager.SingleInstance";
    private const string ActivateEventName = "SteamVrAddonManager.Activate";
    private const int AsfwAny = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private Thread? _listener;
    private volatile bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle activateEvent)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
    }

    /// <summary>
    /// Acquires the single-instance lock. Returns <c>null</c> when another instance is already
    /// running (the existing one is signalled to come forward).
    /// </summary>
    public static SingleInstance? Acquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);

        if (!createdNew)
        {
            // A previous instance exists. Give it a brief moment to exit, so a relaunch-as-admin
            // (which happens right after the old instance closes) can take over as the primary.
            var acquired = false;
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromMilliseconds(500));
            }
            catch (AbandonedMutexException)
            {
                acquired = true; // the previous owner crashed; we now hold it
            }

            if (!acquired)
            {
                mutex.Dispose();
                SignalExisting();
                return null;
            }
        }

        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName));
    }

    /// <summary>Starts listening for activation requests and brings <paramref name="form"/> forward.</summary>
    public void Listen(Form form)
    {
        _listener = new Thread(() =>
        {
            while (!_disposed && _activateEvent.WaitOne())
                Activate(form);
        })
        {
            IsBackground = true,
            Name = "SingleInstanceListener",
        };
        _listener.Start();
    }

    public void Dispose()
    {
        _disposed = true;

        try
        {
            _activateEvent.Set();
            _listener?.Join(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Shutdown is best-effort.
        }

        _activateEvent.Dispose();

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // Not owned (e.g. released by the OS already).
        }

        _mutex.Dispose();
    }

    private static void SignalExisting()
    {
        try
        {
            // Let the running instance raise itself even though we have foreground focus.
            AllowSetForegroundWindow(AsfwAny);

            using var existing = EventWaitHandle.OpenExisting(ActivateEventName);
            existing.Set();
        }
        catch
        {
            // The other instance went away; nothing to signal.
        }
    }

    private static void Activate(Form form)
    {
        if (form.IsDisposed || !form.IsHandleCreated)
            return;

        try
        {
            form.BeginInvoke((Action)(() =>
            {
                if (form.WindowState == FormWindowState.Minimized)
                    form.WindowState = FormWindowState.Normal;

                form.Show();
                form.Activate();
                SetForegroundWindow(form.Handle);
            }));
        }
        catch
        {
            // The handle disappeared between the check and the invoke.
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
}
