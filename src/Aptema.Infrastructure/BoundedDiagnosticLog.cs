using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Aptema.Infrastructure;

public sealed class BoundedDiagnosticLog
{
    private readonly string _directory;
    private readonly long _maxBytes;
    private readonly int _retainedFiles;
    private readonly object _gate = new();

    public BoundedDiagnosticLog(string? directory = null, long maxBytes = 256 * 1024, int retainedFiles = 3)
    {
        _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Aptema", "logs");
        _maxBytes = Math.Max(256, maxBytes);
        _retainedFiles = Math.Clamp(retainedFiles, 1, 8);
    }

    public void Write(string subsystem, string severity, string code)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_directory);
            var current = Path.Combine(_directory, "aptema.jsonl");
            var line = JsonSerializer.Serialize(new DiagnosticLine(
                DateTimeOffset.UtcNow,
                Sanitize(subsystem),
                Sanitize(severity),
                Sanitize(code))) + Environment.NewLine;
            if (File.Exists(current) && new FileInfo(current).Length + line.Length > _maxBytes)
            {
                Rotate(current);
            }

            File.AppendAllText(current, line);
        }
    }

    private void Rotate(string current)
    {
        for (var index = _retainedFiles - 1; index >= 1; index--)
        {
            var source = Path.Combine(_directory, index == 1 ? "aptema.jsonl" : $"aptema.{index - 1}.jsonl");
            var target = Path.Combine(_directory, $"aptema.{index}.jsonl");
            if (File.Exists(source)) File.Move(source, target, overwrite: true);
        }

        if (_retainedFiles == 1 && File.Exists(current)) File.Delete(current);
    }

    internal static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        if (value.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
            return $"Redacted-{hash}";
        }

        var safe = new string(value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.').Take(48).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "Unknown" : safe;
    }

    private sealed record DiagnosticLine(DateTimeOffset At, string Subsystem, string Severity, string Code);
}
