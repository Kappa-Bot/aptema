using System.IO.Compression;
using System.Text.Json;

namespace Aptema.Infrastructure;

public sealed record SupportBundleSnapshot(string AppVersion, bool IsDegraded, IReadOnlyList<string> IssueCodes);

public sealed class SupportBundleService
{
    private readonly string _logsDirectory;

    public SupportBundleService(string? logsDirectory = null)
    {
        _logsDirectory = logsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aptema", "logs");
    }

    public async ValueTask CreateAsync(string outputPath, SupportBundleSnapshot snapshot, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
        Directory.CreateDirectory(directory);
        var temp = outputPath + ".tmp";
        if (File.Exists(temp)) File.Delete(temp);
        try
        {
            await using var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.Asynchronous);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            var diagnostics = archive.CreateEntry("diagnostics.json", CompressionLevel.Optimal);
            await using (var entry = diagnostics.Open())
            {
                await JsonSerializer.SerializeAsync(entry, new
                {
                    product = "Aptema",
                    version = BoundedDiagnosticLog.Sanitize(snapshot.AppVersion),
                    degraded = snapshot.IsDegraded,
                    issues = snapshot.IssueCodes.Select(BoundedDiagnosticLog.Sanitize).Distinct().Take(32).ToArray(),
                    privacy = "No screenshots, window titles, process names, settings, or personal paths included."
                }, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (Directory.Exists(_logsDirectory))
            {
                foreach (var file in Directory.GetFiles(_logsDirectory, "*.jsonl").OrderBy(Path.GetFileName).Take(3))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(file, $"logs/{Path.GetFileName(file)}", CompressionLevel.Optimal);
                }
            }

            archive.Dispose();
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            await stream.DisposeAsync().ConfigureAwait(false);
            File.Move(temp, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
