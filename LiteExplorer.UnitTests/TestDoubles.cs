using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace LiteExplorer.UnitTests;

internal static class TestSetup
{
    [ModuleInitializer]
    internal static void KeepRealLogsClean() =>
        CrashLogger.Redirect(Path.Combine(Path.GetTempPath(), "zerach-test-logs", Guid.NewGuid().ToString("N")));
}

/// <summary>Runs asynchronous test code on a thread with a WPF dispatcher, as the application does.</summary>
internal static class Ui
{
    public static void Run(Func<Task> body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await body();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
    }

    public static async Task Until(Func<bool> condition)
    {
        for (int i = 0; i < 500; i++)
        {
            if (condition()) return;
            await Task.Delay(10);
        }
        throw new TimeoutException("The expected state was not reached.");
    }
}

public class FakeDialogs : IDialogService
{
    public List<string> Messages { get; } = new();
    public bool ConfirmAnswer { get; set; }
    public string? TextAnswer { get; set; }
    public string? FolderAnswer { get; set; }

    public void Info(string message, string title) => Messages.Add(message);
    public void Warn(string message, string title) => Messages.Add(message);
    public void Error(string message, string title) => Messages.Add(message);

    public bool Confirm(string message, string title)
    {
        Messages.Add(message);
        return ConfirmAnswer;
    }

    public string? AskText(string prompt, string title, string initialValue) => TextAnswer;

    public bool AskNewFolder(string parentDirectory, [NotNullWhen(true)] out string? name) => (name = FolderAnswer) != null;

    public ConflictResolutionResult AskConflict(string fileName, string existingPath, string incomingPath) => new();
    public bool AuthenticateAdmin() => false;
    public bool ShowAdminSettings() => false;
    public void ShowAbout() { }
}

public class FakeClipboard : IClipboardService
{
    public IReadOnlyList<string> Paths { get; set; } = Array.Empty<string>();
    public bool IsCut { get; set; }

    public bool HasFiles() => Paths.Count > 0;

    public bool Write(IReadOnlyCollection<string> paths, bool isCut)
    {
        Paths = paths.ToList();
        IsCut = isCut;
        return true;
    }

    public IReadOnlyList<string> Read(out bool isCut)
    {
        isCut = IsCut;
        return Paths;
    }

    public void Clear() => Paths = Array.Empty<string>();
}

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
        if (method!.ReturnType == typeof(PathPermissions)) return PathPermissions.CreateStandard();
        if (method.ReturnType == typeof(IEnumerable<DriveInfo>)) return Array.Empty<DriveInfo>();
        if (method.ReturnType == typeof(ISet<string>)) return new HashSet<string>();
        if (method.ReturnType != typeof(bool)) return null;

        bool denied = DeniedFragment != null && args![0] is string path && path.Contains(DeniedFragment);
        args![^1] = denied ? "denied" : null;
        return !denied;
    }
}

public class RecordingAudit : IAuditService
{
    public List<(AuditEventType Type, string Target, bool Success)> Entries { get; } = new();
    public string LogFilePath => string.Empty;
    public void SetLimits(int retentionDays, int maxSizeMegabytes) { }
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
