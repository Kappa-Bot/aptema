using System.IO.Compression;
using Aptema.Infrastructure;

namespace Aptema.Infrastructure.Tests;

public sealed class DiagnosticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AptemaDiagnosticsTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void LogSanitizesValuesAndStaysBounded()
    {
        var log = new BoundedDiagnosticLog(Path.Combine(_root, "logs"), maxBytes: 350, retainedFiles: 2);

        for (var index = 0; index < 40; index++)
        {
            log.Write("display/path/user", "warning", $"DDC failed C:\\Users\\private\\secret-{index}");
        }

        var files = Directory.GetFiles(Path.Combine(_root, "logs"), "*.jsonl");
        var text = string.Join("", files.Select(File.ReadAllText));
        Assert.True(files.Length <= 2);
        Assert.DoesNotContain("Users", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", text);
    }

    [Fact]
    public async Task SupportBundleContainsOnlySanitizedDiagnostics()
    {
        var logs = Path.Combine(_root, "logs");
        var log = new BoundedDiagnosticLog(logs);
        log.Write("display", "degraded", "DdcUnavailable");
        var bundlePath = Path.Combine(_root, "support.zip");
        var service = new SupportBundleService(logs);

        await service.CreateAsync(bundlePath, new SupportBundleSnapshot("0.4.0", true, ["DdcUnavailable"]), CancellationToken.None);

        using var archive = ZipFile.OpenRead(bundlePath);
        Assert.Contains(archive.Entries, entry => entry.FullName == "diagnostics.json");
        Assert.All(archive.Entries, entry => Assert.DoesNotContain("settings", entry.FullName, StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(archive.GetEntry("diagnostics.json")!.Open());
        var text = await reader.ReadToEndAsync();
        Assert.DoesNotContain(Environment.UserName, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret.exe", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users", text, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
