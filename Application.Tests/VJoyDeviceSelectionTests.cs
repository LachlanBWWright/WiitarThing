using NintrollerLib;
using Shared;
using WiinUSoft;
using WiinUSoft.Services;
using Xunit;

namespace Application.Tests;

public sealed class VJoyDeviceSelectionTests
{
    [Fact]
    public void TwoGuitarsUseDifferentPersistedVJoyIds()
    {
        var configuration = new FakeConfiguration { VirtualOutputMode = VirtualOutputMode.VJoyExperimental };
        var first = new Property("first") { vJoyDeviceId = 101 };
        var second = new Property("second") { vJoyDeviceId = 102 };
        var firstBackend = new FakeBackend(VirtualOutputMode.VJoyExperimental);
        var secondBackend = new FakeBackend(VirtualOutputMode.VJoyExperimental);
        using var firstOutput = new VirtualOutputSession(configuration, () => first, _ => firstBackend);
        using var secondOutput = new VirtualOutputSession(configuration, () => second, _ => secondBackend);
        Assert.True(firstOutput.Connect(1, ControllerType.Guitar).IsOk);
        Assert.True(secondOutput.Connect(2, ControllerType.Guitar).IsOk);
        Assert.Equal(101, firstBackend.Slot);
        Assert.Equal(102, secondBackend.Slot);
    }

    [Fact]
    public void DuplicateIdIsRejectedAndReleasedOnDisconnect()
    {
        var configuration = new FakeConfiguration { VirtualOutputMode = VirtualOutputMode.VJoyExperimental };
        var properties = new Property("guitar") { vJoyDeviceId = 103 };
        var firstBackend = new FakeBackend(VirtualOutputMode.VJoyExperimental);
        var secondBackend = new FakeBackend(VirtualOutputMode.VJoyExperimental);
        using var firstOutput = new VirtualOutputSession(configuration, () => properties, _ => firstBackend);
        using var secondOutput = new VirtualOutputSession(configuration, () => properties, _ => secondBackend);
        Assert.True(firstOutput.Connect(1, ControllerType.Guitar).IsOk);
        var duplicate = secondOutput.Connect(2, ControllerType.Guitar);
        Assert.True(duplicate.IsError);
        Assert.Equal(VirtualControllerErrorKind.ConnectionFailed, duplicate.Error.Kind);
        Assert.Equal(0, secondBackend.Slot);
        firstOutput.Disconnect();
        // A fresh backend is needed after the failed attempt disposed its resources.
        using var replacement = new VirtualOutputSession(configuration, () => properties,
            _ => new FakeBackend(VirtualOutputMode.VJoyExperimental));
        Assert.True(replacement.Connect(2, ControllerType.Guitar).IsOk);
    }

    [Fact]
    public void FailedBackendAcquisitionReleasesTheId()
    {
        var configuration = new FakeConfiguration { VirtualOutputMode = VirtualOutputMode.VJoyExperimental };
        var properties = new Property("guitar") { vJoyDeviceId = 104 };
        using var failing = new VirtualOutputSession(configuration, () => properties,
            _ => new FakeBackend(VirtualOutputMode.VJoyExperimental) { FailConnect = true });
        Assert.True(failing.Connect(1, ControllerType.Guitar).IsError);
        using var recovery = new VirtualOutputSession(configuration, () => properties,
            _ => new FakeBackend(VirtualOutputMode.VJoyExperimental));
        Assert.True(recovery.Connect(2, ControllerType.Guitar).IsOk);
    }
}
