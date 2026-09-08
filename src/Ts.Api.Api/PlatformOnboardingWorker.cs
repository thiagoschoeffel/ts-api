using Ts.Api.Application.Organizations;

namespace Ts.Api.Api;

public sealed class PlatformOnboardingWorker(IServiceScopeFactory scopeFactory,
    ILogger<PlatformOnboardingWorker> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = false;
                while (!stoppingToken.IsCancellationRequested)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<PlatformOnboardingProcessor>();
                    var operationId = await processor.ClaimNextAsync(workerId, stoppingToken);
                    if (!operationId.HasValue) break;
                    processedAny = true;
                    await processor.ProcessAsync(operationId.Value, stoppingToken);
                }
                if (!processedAny) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha no ciclo do worker de onboarding {WorkerId}", workerId);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
