using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Models.ConfigClass;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CostVision.Infrastructure.Services.DataBase;

/// <summary>Допускает запуск HTTP и фонового refresh только после обслуживания и async cleanup.</summary>
public class DatabaseMaintenanceStartup(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseMaintenanceSettings> settings,
    ILogger<DatabaseMaintenanceStartup> logger)
{
    private int _started;
    private bool _isReady;

    public bool IsReady => Volatile.Read(ref _isReady);

    /// <summary>Одна попытка без retry; ошибки журналируются только закрытыми кодами.</summary>
    public async Task<bool> RunAsync(CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("Повторный запуск обслуживания в этом процессе запрещён.");
        }

        DatabaseMaintenanceResult result;
        try
        {
            DatabaseMaintenanceSettings options = settings.Value;
            bool validTimeout = options.TimeoutSeconds is > 0 and <= 3600;
            if (!validTimeout || !options.SingleInitializerConfirmed || string.IsNullOrWhiteSpace(options.ServerBackupDirectory))
            {
                throw new MaintenanceException(MaintenanceError.Configuration);
            }

            if (!Enum.IsDefined(options.Operation))
            {
                throw new MaintenanceException(MaintenanceError.Configuration);
            }

            result = await RunScopedAsync(options, ct);
        }
        catch (MaintenanceException exception)
        {
            logger.LogCritical("Database startup failed: {Code} {PrimaryCode}", exception.Code, exception.PrimaryError);
            return false;
        }
        catch (OperationCanceledException)
        {
            logger.LogCritical("Database startup failed: {Code}", MaintenanceError.Cancelled);
            return false;
        }
        catch (Exception)
        {
            logger.LogCritical("Database startup failed: {Code}", MaintenanceError.Configuration);
            return false;
        }

        if (ct.IsCancellationRequested)
        {
            logger.LogCritical("Database startup failed: {Code}", MaintenanceError.Cancelled);
            return false;
        }

        Volatile.Write(ref _isReady, true);
        logger.LogInformation("Database startup ready: {Outcome}", result.Outcome);

        return true;
    }

    private async Task<DatabaseMaintenanceResult> RunScopedAsync(DatabaseMaintenanceSettings options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        MaintenanceError failureCode = MaintenanceError.Configuration;
        MaintenanceError? primaryCode = null;
        Exception? primaryException = null;
        try
        {
            IDatabaseMaintenance<CostVisionContextKey> maintenance = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenance<CostVisionContextKey>>();
            TimeSpan timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            failureCode = MaintenanceError.ConnectionFailed;
            if (options.Operation == DatabaseStartupOperation.InitializeNew)
            {
                return await maintenance.InitializeNewAsync(timeout, ct);
            }

            return await maintenance.UpdateExistingAsync(timeout, ct);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            if (exception is MaintenanceException known)
            {
                primaryCode = known.PrimaryError ?? known.Code;
            }
            else if (exception is OperationCanceledException)
            {
                primaryCode = MaintenanceError.Cancelled;
            }
            else
            {
                primaryCode = failureCode;
                throw new MaintenanceException(failureCode, null, exception);
            }

            throw;
        }
        finally
        {
            try
            {
                await scope.DisposeAsync();
            }
            catch (Exception cleanup)
            {
                Exception diagnostic = cleanup;
                if (primaryException != null)
                {
                    diagnostic = new AggregateException(primaryException, cleanup);
                }

                throw new MaintenanceException(MaintenanceError.CleanupUnconfirmed, primaryCode, diagnostic);
            }
        }
    }
}
