using LightPilot.App.Services;
using LightPilot.Application;
using LightPilot.Core;

namespace LightPilot.App.Tests;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void RegistersOnlyQuickAdjustByDefault()
    {
        var registrar = new RecordingRegistrar(succeeds: true);
        using var service = new GlobalHotkeyService(registrar);

        var result = service.RegisterDefaults();

        Assert.Equal(OperationStatus.Success, result.Status);
        var call = Assert.Single(registrar.Registrations);
        Assert.Equal(HotkeyAction.QuickAdjust, call.Action);
        Assert.Equal(HotkeyModifiers.Windows | HotkeyModifiers.Alt | HotkeyModifiers.NoRepeat, call.Modifiers);
        Assert.Equal('A', call.VirtualKey);
    }

    [Fact]
    public void ReportsConflictWhenWindowsRejectsRegistration()
    {
        using var service = new GlobalHotkeyService(new RecordingRegistrar(succeeds: false));

        var result = service.RegisterDefaults();

        Assert.Equal(OperationStatus.Conflict, result.Status);
        Assert.Equal("QuickAdjustHotkeyUnavailable", result.Code);
    }

    [Fact]
    public void RegistersConfiguredQuickAdjustGesture()
    {
        var registrar = new RecordingRegistrar(succeeds: true);
        using var service = new GlobalHotkeyService(registrar);

        var result = service.RegisterConfigured(new HotkeyConfiguration("Ctrl+Shift+Q", null, null, null, null, null));

        Assert.Equal(OperationStatus.Success, result.Status);
        var call = Assert.Single(registrar.Registrations);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.NoRepeat, call.Modifiers);
        Assert.Equal('Q', call.VirtualKey);
    }

    [Fact]
    public void BlankQuickAdjustGestureLeavesHotkeyDisabledWithoutConflict()
    {
        var registrar = new RecordingRegistrar(succeeds: true);
        using var service = new GlobalHotkeyService(registrar);

        var result = service.RegisterConfigured(new HotkeyConfiguration(null, null, null, null, null, null));

        Assert.Equal(OperationStatus.Unavailable, result.Status);
        Assert.Empty(registrar.Registrations);
    }

    private sealed class RecordingRegistrar(bool succeeds) : IHotkeyRegistrar
    {
        public List<(int Id, HotkeyAction Action, HotkeyModifiers Modifiers, int VirtualKey)> Registrations { get; } = [];

        public bool Register(int id, HotkeyAction action, HotkeyModifiers modifiers, int virtualKey)
        {
            Registrations.Add((id, action, modifiers, virtualKey));
            return succeeds;
        }

        public void Unregister(int id) { }
    }
}
