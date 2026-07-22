using System.IO.Compression;
using System.Security.Cryptography;

namespace Aptema.Updater;

public sealed record UpdateManifest(string Version, string PackageFileName, string Sha256);

public sealed record UpdateResult(bool Succeeded, bool RolledBack, string Code)
{
    public static UpdateResult Success() => new(true, false, "UpdateApplied");
    public static UpdateResult Failure(string code, bool rolledBack = false) => new(false, rolledBack, code);
}

public interface IUpdateProbe
{
    ValueTask<bool> IsHealthyAsync(string executablePath, CancellationToken cancellationToken);
}

public sealed class UpdateEngine(IUpdateProbe probe)
{
    public async ValueTask<UpdateResult> ApplyAsync(
        string packagePath,
        UpdateManifest manifest,
        string installDirectory,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath) || string.IsNullOrWhiteSpace(installDirectory))
            return UpdateResult.Failure("UpdateInputUnavailable");
        if (!string.Equals(Path.GetFileName(packagePath), manifest.PackageFileName, StringComparison.OrdinalIgnoreCase))
            return UpdateResult.Failure("PackageNameMismatch");
        if (!await ChecksumMatchesAsync(packagePath, manifest.Sha256, cancellationToken).ConfigureAwait(false))
            return UpdateResult.Failure("ChecksumMismatch");

        var install = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var parent = Directory.GetParent(install)?.FullName;
        if (string.IsNullOrWhiteSpace(parent)) return UpdateResult.Failure("InstallPathInvalid");
        var next = install + ".next";
        var previous = install + ".previous";
        var movedCurrent = false;

        try
        {
            DeleteDirectory(next);
            ExtractSecure(packagePath, next);
            var stagedExecutable = FindAppExecutable(next);
            if (stagedExecutable is null) return UpdateResult.Failure("ApplicationMissingFromPackage");
            if (!await probe.IsHealthyAsync(stagedExecutable, cancellationToken).ConfigureAwait(false))
                return UpdateResult.Failure("StagedSmokeFailed");

            DeleteDirectory(previous);
            if (Directory.Exists(install))
            {
                Directory.Move(install, previous);
                movedCurrent = true;
            }

            Directory.Move(next, install);
            var installedExecutable = FindAppExecutable(install)!;
            if (!await probe.IsHealthyAsync(installedExecutable, cancellationToken).ConfigureAwait(false))
            {
                Rollback(install, previous, movedCurrent);
                return UpdateResult.Failure("InstalledSmokeFailed", rolledBack: movedCurrent);
            }

            return UpdateResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            if (movedCurrent) Rollback(install, previous, movedCurrent: true);
            return UpdateResult.Failure("UpdateApplyFailed", rolledBack: movedCurrent);
        }
        finally
        {
            DeleteDirectory(next);
        }
    }

    private static async ValueTask<bool> ChecksumMatchesAsync(string path, string expected, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expected) || expected.Length != 64) return false;
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void ExtractSecure(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive entry escapes staging directory.");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static string? FindAppExecutable(string root) =>
        Directory.EnumerateFiles(root, "Aptema.App.exe", SearchOption.AllDirectories).SingleOrDefault();

    private static void Rollback(string install, string previous, bool movedCurrent)
    {
        DeleteDirectory(install);
        if (movedCurrent && Directory.Exists(previous)) Directory.Move(previous, install);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
