using PlatformVault.Application.Abstractions;
using PlatformVault.Application.Jobs;

namespace PlatformVault.Api.Infrastructure;

public sealed class JobOptions
{
    public const string Section = "Jobs";

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Ejecuta los trabajos en segundo plano dentro de la API (IMP-13). Cada ejecución toma un bloqueo sp_getapplock,
/// de modo que con varias instancias solo una trabaja, y queda registrada en app.JobRun.
/// </summary>
public sealed class JobScheduler(IServiceScopeFactory scopes, ILogger<JobScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        List<(string Name, TimeSpan Interval, Type Type)> jobs;
        await using (var scope = scopes.CreateAsyncScope())
        {
            jobs = scope.ServiceProvider.GetServices<IBackgroundJob>().Select(j => (j.Name, j.Interval, j.GetType())).ToList();
        }
        await Task.WhenAll(jobs.Select(j => LoopAsync(j.Name, j.Interval, j.Type, stoppingToken)));
    }

    private async Task LoopAsync(string name, TimeSpan interval, Type jobType, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        do
        {
            await RunOnceAsync(name, jobType, ct);
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }

    private async Task RunOnceAsync(string name, Type jobType, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var jobLock = services.GetRequiredService<IJobLock>();
            await using var lease = await jobLock.TryAcquireAsync(name, ct);
            if (lease is null)
                return;

            var runs = services.GetRequiredService<IJobRunRepository>();
            var clock = services.GetRequiredService<IClock>();
            var runId = await runs.StartAsync(name, clock.UtcNow, ct);
            try
            {
                var job = (IBackgroundJob)services.GetRequiredService(jobType);
                var processed = await job.RunAsync(ct);
                await runs.FinishAsync(runId, "Succeeded", processed, null, clock.UtcNow, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falló el trabajo {Job}", name);
                await runs.FinishAsync(runId, "Failed", 0, ex.GetType().Name, clock.UtcNow, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo ejecutar el trabajo {Job}", name);
        }
    }
}
