using CurrencyRates.Rates.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<RatesWorker>();

await builder.Build().RunAsync();
