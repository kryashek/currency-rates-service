using CurrencyRates.DbMigrator;

return await MigrationRunner.RunAsync(Environment.GetEnvironmentVariable("ConnectionStrings__Migrator"));
