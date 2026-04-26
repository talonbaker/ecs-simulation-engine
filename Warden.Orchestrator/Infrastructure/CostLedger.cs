using System.Text.Json;
using Warden.Contracts;

namespace Warden.Orchestrator.Infrastructure;

/// <summary>
/// Append-only JSONL ledger. Each call to <see cref="AppendAsync"/> writes one
/// <see cref="LedgerEntry"/> line to <c>cost-ledger.jsonl</c>.
/// </summary>
public sealed class CostLedger
{
    private readonly string _ledgerPath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <param name="ledgerPath">Absolute path to the <c>.jsonl</c> file. Created if absent.</param>
    public CostLedger(string ledgerPath)
    {
        _ledgerPath = ledgerPath;

        var dir = Path.GetDirectoryName(ledgerPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    /// <summary>Appends one ledger entry as a JSON line.</summary>
    public async Task AppendAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions.Wire) + "\n";
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(_ledgerPath, line, ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Reads all entries written so far. Used by tests and the report aggregator.</summary>
    public async Task<IReadOnlyList<LedgerEntry>> ReadAllAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_ledgerPath))
            return Array.Empty<LedgerEntry>();

        var lines = await File.ReadAllLinesAsync(_ledgerPath, ct).ConfigureAwait(false);
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<LedgerEntry>(l, JsonOptions.Wire)!)
            .ToList()
            .AsReadOnly();
    }
}
