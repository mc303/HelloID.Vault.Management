using System.IO;
using System.Text;

namespace HelloID.Vault.Services.ActiveDirectory;

/// <summary>
/// Append-only audit log for AD write operations. Fail-closed: when an audit
/// entry cannot be written, the owning run must not (continue to) modify AD.
/// Every modification is preceded by a PENDING entry, so a modification is
/// never without at least one audit record; the outcome is appended after.
/// </summary>
public sealed class AdAuditLogger : IDisposable
{
    private readonly StreamWriter _writer;

    private AdAuditLogger(string path, StreamWriter writer)
    {
        PathName = path;
        _writer = writer;
    }

    public string PathName { get; }

    /// <summary>
    /// Opens the audit log for appending. Throws when the log cannot be opened
    /// or written to - callers must abort the run before modifying AD.
    /// </summary>
    public static AdAuditLogger Open()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HelloID.Vault.Management", "logs");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "ad-write-audit.log");
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        // Verify the log is actually writable before the caller touches AD
        writer.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd'T'HH:mm:ss'Z'} | RUN-START");
        writer.Flush();

        return new AdAuditLogger(path, writer);
    }

    /// <summary>Writes one audit line. Returns false when writing failed (caller must halt the run).</summary>
    public bool TryWrite(string line)
    {
        try
        {
            _writer.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd'T'HH:mm:ss'Z'} | {line}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdAudit] FAILED to write audit line: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            _writer.Dispose();
        }
        catch
        {
            // Closing the audit log must never throw
        }
    }
}
