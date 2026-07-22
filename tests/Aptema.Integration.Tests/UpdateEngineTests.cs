using System.IO.Compression;
using System.Security.Cryptography;
using Aptema.Updater;

namespace Aptema.Integration.Tests;

public sealed class UpdateEngineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AptemaUpdaterTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RejectsWrongChecksumBeforeSmoke()
    {
        var package = CreatePackage("new");
        var probe = new SequenceProbe(true);

        var result = await new UpdateEngine(probe).ApplyAsync(package, new("0.4.0", Path.GetFileName(package), new string('0', 64)), Path.Combine(_root, "App"), CancellationToken.None);

        Assert.Equal("ChecksumMismatch", result.Code);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task AppliesHealthyPackageAndKeepsKnownGoodCopy()
    {
        var install = CreateCurrentInstall();
        var package = CreatePackage("new");
        var result = await new UpdateEngine(new SequenceProbe(true, true)).ApplyAsync(package, Manifest(package), install, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("new", File.ReadAllText(Path.Combine(install, "marker.txt")));
        Assert.Equal("old", File.ReadAllText(Path.Combine(install + ".previous", "marker.txt")));
    }

    [Fact]
    public async Task FailedInstalledSmokeRestoresPreviousVersion()
    {
        var install = CreateCurrentInstall();
        var package = CreatePackage("new");
        var result = await new UpdateEngine(new SequenceProbe(true, false)).ApplyAsync(package, Manifest(package), install, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.RolledBack);
        Assert.Equal("old", File.ReadAllText(Path.Combine(install, "marker.txt")));
    }

    private string CreateCurrentInstall()
    {
        var install = Path.Combine(_root, "App");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "Aptema.App.exe"), "old-exe");
        File.WriteAllText(Path.Combine(install, "marker.txt"), "old");
        return install;
    }

    private string CreatePackage(string marker)
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Aptema.App.exe"), "new-exe");
        File.WriteAllText(Path.Combine(source, "marker.txt"), marker);
        var package = Path.Combine(_root, "Aptema-0.4.0-win-x64.zip");
        ZipFile.CreateFromDirectory(source, package);
        Directory.Delete(source, recursive: true);
        return package;
    }

    private static UpdateManifest Manifest(string package)
    {
        using var stream = File.OpenRead(package);
        return new UpdateManifest("0.4.0", Path.GetFileName(package), Convert.ToHexString(SHA256.HashData(stream)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class SequenceProbe(params bool[] results) : IUpdateProbe
    {
        private readonly Queue<bool> _results = new(results);
        public int Calls { get; private set; }
        public ValueTask<bool> IsHealthyAsync(string executablePath, CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(_results.Dequeue());
        }
    }
}
