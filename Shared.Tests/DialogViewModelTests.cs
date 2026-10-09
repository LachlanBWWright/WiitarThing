using System.Collections.Generic;
using WiinUSoft.ViewModels;
using Xunit;

namespace Shared.Tests;

public sealed class DialogViewModelTests
{
    [Fact]
    public void MappingViewModelResetsAndTrimsTargets()
    {
        var model = new ControllerMappingViewModel(new Dictionary<string, string>
        {
            ["A"] = "  X  ",
            ["B"] = "Y"
        });

        Assert.Equal("X", model.ToDictionary()["A"]);
        model.ResetToDefault();

        Assert.Equal("A", model.ToDictionary()["A"]);
        Assert.Equal("B", model.ToDictionary()["B"]);
    }

    [Fact]
    public void SyncViewModelTracksOutputAndCompletionState()
    {
        var model = new SyncViewModel();
        model.Start();
        model.AppendOutput("Searching...");
        model.AddDevice();
        model.Stop(foundDevice: true, compatibleRadio: true);

        Assert.Contains("Searching...", model.Output);
        Assert.Equal(1, model.Count);
        Assert.False(model.IsRunning);
        Assert.Equal("OK", model.CloseButtonText);
    }

    [Fact]
    public void SyncViewModelUsesOkForIncompatibleRadio()
    {
        var model = new SyncViewModel();
        model.Start();
        model.Stop(foundDevice: false, compatibleRadio: false);

        Assert.Equal("OK", model.CloseButtonText);
    }
}
