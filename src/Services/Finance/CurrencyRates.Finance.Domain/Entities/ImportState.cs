namespace CurrencyRates.Finance.Domain.Entities;

public sealed class ImportState
{
    public int Id { get; set; } = 1;
    public ulong SnapshotVersion { get; set; }
    public DateOnly? SourceDate { get; set; }
    public DateTimeOffset? LoadedAt { get; set; }
}
