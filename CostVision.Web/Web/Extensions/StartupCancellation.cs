using System.Runtime.InteropServices;

namespace CostVision.Web.Extensions;

/// <summary>Передаёт сигналы завершения в startup до регистрации ConsoleLifetime хостом.</summary>
internal class StartupCancellation : IDisposable
{
    private readonly CancellationTokenSource _source;
    private readonly PosixSignalRegistration? _termination;
    private readonly object _sync = new();
    private bool _disposed;

    public CancellationToken Token => _source.Token;

    public StartupCancellation(CancellationToken applicationStopping)
    {
        _source = CancellationTokenSource.CreateLinkedTokenSource(applicationStopping);
        try
        {
            Console.CancelKeyPress += OnCancelKeyPress;
            if (!OperatingSystem.IsWindows())
            {
                _termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnTermination);
            }
        }
        catch
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            _source.Dispose();
            throw;
        }
    }

    internal void Cancel()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _source.Cancel();
            }
        }
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        Cancel();
    }

    private void OnTermination(PosixSignalContext signal)
    {
        signal.Cancel = true;
        Cancel();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Console.CancelKeyPress -= OnCancelKeyPress;
        _termination?.Dispose();
        _source.Dispose();
    }
}
