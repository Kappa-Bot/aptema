using System.Diagnostics;
using System.Text.Json;
using Aptema.Updater;

var arguments = args
    .Select((value, index) => (value, index))
    .Where(item => item.value.StartsWith("--", StringComparison.Ordinal))
    .ToDictionary(item => item.value, item => item.index + 1 < args.Length ? args[item.index + 1] : "", StringComparer.OrdinalIgnoreCase);
if (!arguments.TryGetValue("--package", out var package) ||
    !arguments.TryGetValue("--manifest", out var manifestPath) ||
    !arguments.TryGetValue("--install-dir", out var installDirectory))
{
    return 2;
}

try
{
    var manifest = JsonSerializer.Deserialize<UpdateManifest>(await File.ReadAllTextAsync(manifestPath));
    if (manifest is null) return 3;
    var result = await new UpdateEngine(new ProcessUpdateProbe()).ApplyAsync(package, manifest, installDirectory, CancellationToken.None);
    return result.Succeeded ? 0 : result.RolledBack ? 10 : 4;
}
catch
{
    return 5;
}

internal sealed class ProcessUpdateProbe : IUpdateProbe
{
    public async ValueTask<bool> IsHealthyAsync(string executablePath, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo(executablePath, "--smoke-test --safe-mode --no-hardware")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath)
        });
        if (process is null) return false;
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return process.ExitCode == 0;
        }
        catch (TimeoutException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return false;
        }
    }
}
