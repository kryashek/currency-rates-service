namespace CurrencyRates.Rates.Worker;

public sealed class RatesWorker(ILogger<RatesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Rates Worker scaffold started. Currency synchronization is not implemented yet.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
