namespace CurrencyRates.Finance.Domain.Entities;

public sealed class ImportBatch
{
    public Guid BatchId { get; set; }
    public byte[] RequestHash { get; set; } = [];
    public ulong ExpectedSnapshotVersion { get; set; }
    public DateOnly SourceDate { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public ImportOutcome Outcome { get; set; }
    public ulong SnapshotVersion { get; set; }
    public DateOnly StateSourceDate { get; set; }
    public DateTimeOffset StateLoadedAt { get; set; }
}
