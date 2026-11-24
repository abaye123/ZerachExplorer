using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.IO;
using System.Reflection;

namespace LiteExplorer.UnitTests;

/// <summary>Policy that permits everything except paths containing <see cref="DeniedFragment"/>.</summary>
public class StubPolicy : DispatchProxy
{
    public string? DeniedFragment { get; set; }

    public static IPathPolicy Create(string? deniedFragment = null)
    {
        var proxy = Create<IPathPolicy, StubPolicy>();
        ((StubPolicy)(object)proxy).DeniedFragment = deniedFragment;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.ReturnType != typeof(bool)) return null;

        bool denied = DeniedFragment != null && args![0] is string path && path.Contains(DeniedFragment);
        args![^1] = denied ? "denied" : null;
        return !denied;
    }
}

public class RecordingAudit : IAuditService
{
    public List<(AuditEventType Type, string Target, bool Success)> Entries { get; } = new();
    public string LogFilePath => string.Empty;
    public void Log(AuditEventType eventType, string target, string? details = null, bool success = true) => Entries.Add((eventType, target, success));
    public IReadOnlyList<AuditEntry> GetRecentEntries(int count = 100) => Array.Empty<AuditEntry>();
    public void ClearLogs() => Entries.Clear();
}

public sealed class TempFolder : IDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("zerach-test-").FullName;

    public string Write(string relative, string content)
    {
        string path = At(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string Read(string relative) => File.ReadAllText(At(relative));
    public bool Exists(string relative) => Path.Exists(At(relative));
    public string At(string relative) => Path.Combine(Root, relative);
    public void Dispose() => Directory.Delete(Root, true);
}
