using System.Collections.Generic;
using WiinUSoft.ViewModels;
using Xunit;

namespace Shared.Tests;

public class ControllerMappingViewModelTests
{
    [Fact]
    public void ResetToDefaultCopiesSourceKeysToTargets()
    {
        var source = new Dictionary<string, string>
        {
            ["A"] = "ButtonSouth",
            ["B"] = "ButtonEast"
        };

        var viewModel = new ControllerMappingViewModel(source);
        viewModel.ResetToDefault();

        var map = viewModel.ToDictionary();
        Assert.Equal("A", map["A"]);
        Assert.Equal("B", map["B"]);
    }

    [Fact]
    public void ToDictionaryTrimsTargetValues()
    {
        var source = new Dictionary<string, string>
        {
            ["A"] = "  ButtonSouth  "
        };

        var viewModel = new ControllerMappingViewModel(source);
        var map = viewModel.ToDictionary();

        Assert.Equal("ButtonSouth", map["A"]);
    }
}
