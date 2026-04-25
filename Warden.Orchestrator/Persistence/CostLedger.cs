using System.Text;
using System.Text.Json;

namespace Warden.Orchestrator.Persistence;

/// <summary>
/// Append-only JSONL ledger; one line per API call.
/// Thread-safe via a single <see cref="SemaphoreSlim"/>; writes are serial
/// (~1 ms each) and burst-bounded by the 30-call concurrency cap.
/// </summary>
public sealed class CostLedger : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly FileStream _stream;
    private bool _disposed;

    private CostLedger(FileStream stream) => _stream = stream;

    /// <summary>
    /// Opens (or creates) the ledger file at <paramref name="path"/> for appending.
    /// The file is kept open for the lifetime of the ledger instance.
    /// </summary>
    public static CostLedger Open(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        return new CostLedger(stream);
    }

    /// <summary>
    /// Serialises <paramref name="entry"/> as a single JSONL line and fsyncs to disk.
    /// Serialisation happens under the semaphore; lines are never interleaved.
    /// </summary>
    public async Task WriteAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json  = JsonSerializer.Serialize(entry);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            await _stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            _stream.Flush(true); // fsync: crash-safe write
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
        _stream.Dispose();
    }
}
