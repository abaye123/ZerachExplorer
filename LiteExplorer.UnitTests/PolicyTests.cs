using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Xunit;

namespace LiteExplorer.UnitTests;

public class PolicyTests
{
    [Fact]
    public void ContainerProvidesPolicyWithDefaultExtensions()
    {
        var services = new ServiceCollection();
        App.ConfigureServices(services);
        using var provider = services.BuildServiceProvider();

        var policy = provider.GetRequiredService<IPathPolicy>();

        Assert.Contains(".docx", policy.AllowedExtensions);
        Assert.Contains(".pdf", policy.AllowedExtensions);
        Assert.NotNull(provider.GetRequiredService<IFileTransferService>());
        Assert.NotNull(provider.GetRequiredService<IFileDeletionService>());
    }

    [Fact]
    public void SystemDriveStaysBlockedEvenWhenApproved()
    {
        var policy = new PathPolicy();
        policy.AllowedDriveRoots.Add(PathPolicy.SystemDriveRoot);
        policy.AllowedExplicitDirectories.Add(PathPolicy.SystemDriveRoot);

        Assert.False(policy.CanNavigate(PathPolicy.SystemDriveRoot, out _));
    }

    [Fact]
    public void AdministratorBlockWinsOverApprovedFolder()
    {
        string root = UnusedDriveRoot();
        var policy = new PathPolicy();
        policy.AllowedExplicitDirectories.Add(root + "work");
        Assert.True(policy.CanNavigate(root + "work", out _));

        policy.BlockedDriveRoots.Add(root);

        Assert.False(policy.CanNavigate(root + "work", out _));
    }

    [Fact]
    public void ConfigurationCarriesBlockedDrives()
    {
        using var temp = new TempFolder();
        string path = temp.At("config.json");
        var service = new ConfigurationService(path);
        var config = service.Load();
        config.BlockedDriveRoots.Add(@"E:\");
        Assert.True(service.Save(config, out _));

        var policy = new PathPolicy();
        new ConfigurationService(path).ApplyToPolicy(policy);

        Assert.Contains(@"e:\", policy.BlockedDriveRoots);
    }

    [Fact]
    public void DeletionRefusesWhenPolicyDenies()
    {
        using var temp = new TempFolder();
        string file = temp.Write("keep.txt", "x");
        var audit = new RecordingAudit();

        bool deleted = new FileDeletionService(StubPolicy.Create("keep.txt"), audit).DeletePermanently(file, out var error);

        Assert.False(deleted);
        Assert.Equal("denied", error);
        Assert.True(File.Exists(file));
        Assert.Contains(audit.Entries, e => e.Type == AuditEventType.FileDeleted && !e.Success);
    }

    [Fact]
    public void PermanentDeletionRemovesFolder()
    {
        using var temp = new TempFolder();
        temp.Write(@"d\x.txt", "x");

        Assert.True(new FileDeletionService(StubPolicy.Create(), new RecordingAudit()).DeletePermanently(temp.At("d"), out _));
        Assert.False(temp.Exists("d"));
    }

    private static string UnusedDriveRoot()
    {
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        return Enumerable.Range('D', 23).Select(c => (char)c).First(c => !used.Contains(c)) + @":\";
    }
}
