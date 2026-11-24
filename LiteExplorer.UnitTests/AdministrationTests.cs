using LiteExplorer.MVVM.Models;
using LiteExplorer.Services;
using System.ComponentModel;
using System.IO;
using Xunit;

namespace LiteExplorer.UnitTests;

public sealed class AdministrationTests : IDisposable
{
    private readonly TempFolder temp = new();
    private DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Dispose() => temp.Dispose();

    private ConfigurationService Create() => new(temp.At("config.json"), () => now);

    private ConfigurationService CreateWithPassword()
    {
        var service = Create();
        var config = service.CreateEditableCopy();
        service.SetPassword(config, "correct-horse");
        Assert.True(service.Save(config, out _));
        return service;
    }

    [Fact]
    public void MissingFileHasNoPasswordAndAcceptsNone()
    {
        var service = Create();

        Assert.False(service.HasPassword);
        Assert.False(service.IsLocked);
        Assert.False(service.VerifyPassword("admin123"));
        Assert.False(service.VerifyPassword(""));
        Assert.False(temp.Exists("config.json"));
    }

    [Theory]
    [InlineData("{ broken")]
    [InlineData("null")]
    [InlineData("{\"AllowedExtensions\":null}")]
    [InlineData("{\"Permissions\":null}")]
    public void DamagedFileDeniesEverything(string content)
    {
        temp.Write("config.json", content);
        var service = Create();
        var policy = new PathPolicy();

        service.ApplyToPolicy(policy);

        Assert.True(service.IsLocked);
        Assert.False(service.VerifyPassword("admin123"));
        Assert.False(policy.Permissions.CanList);
        Assert.Empty(policy.AllowedExtensions);
    }

    [Fact]
    public void RepairStartsFromDefaultsAndUnlocks()
    {
        temp.Write("config.json", "{ broken");
        var service = Create();
        service.Load();

        var config = service.CreateEditableCopy();
        Assert.True(service.Save(config, out _));

        Assert.False(service.IsLocked);
        Assert.True(service.Configuration.Permissions.CanList);
    }

    [Fact]
    public void EditingACopyDoesNotChangeActiveSettings()
    {
        var service = CreateWithPassword();

        service.SetPassword(service.CreateEditableCopy(), "something-else");

        Assert.True(service.VerifyPassword("correct-horse"));
    }

    [Fact]
    public void RepeatedFailuresLockOutEvenTheCorrectPassword()
    {
        var service = CreateWithPassword();
        for (int i = 0; i < 5; i++) Assert.False(service.VerifyPassword("wrong"));

        Assert.False(service.VerifyPassword("correct-horse", out var wait));
        Assert.True(wait > TimeSpan.Zero);

        now += TimeSpan.FromSeconds(31);
        Assert.True(service.VerifyPassword("correct-horse"));
    }

    [Fact]
    public void FileOwnedByRegularUserIsNotTrusted()
    {
        string path = temp.Write("config.json", "{}");
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var info = new FileInfo(path);
        var security = info.GetAccessControl();
        security.SetOwner(identity.User!);
        info.SetAccessControl(security);

        Assert.False(ProtectedConfigurationStore.IsTrusted(path));
    }

    [Fact]
    public void KioskSettingsSurviveSaveAndReachController()
    {
        var service = Create();
        var config = service.CreateEditableCopy();
        config.KioskModeEnabled = config.PreventAppExit = true;
        Assert.True(service.Save(config, out _));

        var controller = new KioskController();
        controller.Configure(Create().Configuration);

        Assert.True(controller.IsKioskModeEnabled);
        Assert.True(controller.PreventAppExit);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosingRealWindowFollowsAuthentication(bool authenticated)
    {
        bool closed = false;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var controller = new KioskController { PreventAppExit = true };
                var window = new System.Windows.Window { Width = 10, Height = 10, Left = -5000, ShowInTaskbar = false, ShowActivated = false };
                CancelEventHandler handler = (_, e) => controller.HandleWindowClosing(window, e, () => authenticated);
                window.Closing += handler;
                window.Closed += (_, _) => closed = true;
                window.Show();
                window.Close();
                if (!closed)
                {
                    window.Closing -= handler;
                    bool before = closed;
                    window.Close();
                    closed = before;
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.Equal(authenticated, closed);
    }
}
