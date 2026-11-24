using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

namespace LiteExplorer.Services;

/// <summary>
/// Stores the configuration where only Windows administrators can change it.
/// A non-elevated process writes through an elevated copy of itself (UAC).
/// </summary>
public static class ProtectedConfigurationStore
{
    public const string ApplyArgument = "--apply-config";
    private const int MaxConfigurationBytes = 1024 * 1024;

    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);

    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ZerachExplorer");

    public static string FilePath { get; } = Path.Combine(DirectoryPath, "config.json");

    /// <summary>
    /// A file created by a regular user must not be trusted: anyone could have planted it.
    /// </summary>
    public static bool IsTrusted(string path)
    {
        var owner = new FileInfo(path).GetAccessControl().GetOwner(typeof(SecurityIdentifier));
        return Administrators.Equals(owner) || LocalSystem.Equals(owner);
    }

    public static bool Save(byte[] content, out string? error)
    {
        error = null;

        if (IsElevated())
        {
            WriteProtected(content);
            return true;
        }

        string temporary = Path.Combine(Path.GetTempPath(), $"zerach-config-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllBytes(temporary, content);

            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            start.ArgumentList.Add(ApplyArgument);
            start.ArgumentList.Add(temporary);
            start.ArgumentList.Add(Hash(content));

            using var process = Process.Start(start);
            process!.WaitForExit();
            if (process.ExitCode == 0)
                return true;

            error = "שמירת ההגדרות נכשלה.";
            return false;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = "השמירה דורשת אישור של מנהל Windows.";
            return false;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>
    /// Entry point of the elevated copy. Returns the process exit code.
    /// </summary>
    public static int RunElevatedSave(string source, string expectedHash)
    {
        try
        {
            if (!IsElevated())
                return 2;

            byte[] content;
            using (var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > MaxConfigurationBytes)
                    return 3;

                content = new byte[stream.Length];
                stream.ReadExactly(content);
            }

            // The temporary file is writable by the user; accept only what was approved.
            if (!string.Equals(Hash(content), expectedHash, StringComparison.OrdinalIgnoreCase))
                return 4;

            ConfigurationService.Parse(content);
            WriteProtected(content);
            return 0;
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, "Elevated configuration save");
            return 1;
        }
    }

    private static void WriteProtected(byte[] content)
    {
        var directory = new DirectoryInfo(DirectoryPath);
        RejectLink(directory);

        if (directory.Exists)
            directory.SetAccessControl(CreateDirectorySecurity());
        else
            directory.Create(CreateDirectorySecurity());

        // Checked again now that regular users can no longer swap the folder.
        directory.Refresh();
        RejectLink(directory);

        File.Delete(FilePath);

        string pending = Path.Combine(DirectoryPath, $"config-{Guid.NewGuid():N}.tmp");
        using (var stream = new FileInfo(pending).Create(FileMode.CreateNew, FileSystemRights.Write, FileShare.None, 4096, FileOptions.WriteThrough, CreateFileSecurity()))
        {
            stream.Write(content);
        }
        File.Move(pending, FilePath);
    }

    private static void RejectLink(DirectoryInfo directory)
    {
        if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The configuration folder is a link.");
    }

    private static DirectorySecurity CreateDirectorySecurity()
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.SetOwner(Administrators);
        return security;
    }

    private static FileSecurity CreateFileSecurity()
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(Administrators, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(LocalSystem, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(Users, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        security.SetOwner(Administrators);
        return security;
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content));
}
