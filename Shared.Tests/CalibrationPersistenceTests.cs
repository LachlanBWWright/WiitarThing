using NintrollerLib;
using Xunit;

namespace Shared.Tests;

public sealed class CalibrationPersistenceTests
{
    [Theory]
    [InlineData(Calibrations.CalibrationPreset.Default)]
    [InlineData(Calibrations.CalibrationPreset.Minimum)]
    [InlineData(Calibrations.CalibrationPreset.Modest)]
    [InlineData(Calibrations.CalibrationPreset.Extra)]
    [InlineData(Calibrations.CalibrationPreset.None)]
    public void PresetsRoundTripWithoutLosingControllerCalibration(Calibrations.CalibrationPreset preset)
    {
        var original = new CalibrationStorage();
        original.SetCalibrations(preset);
        string serialized = original.ToString();
        var restored = new CalibrationStorage(serialized);
        Assert.Equal(serialized, restored.ToString());
    }

    [Fact]
    public void LegacyClassicPrefixStillLoads()
    {
        var original = new CalibrationStorage();
        original.SetCalibrations(Calibrations.CalibrationPreset.Extra);
        var restored = new CalibrationStorage(original.ToString().Replace("-cla:", "-cls:"));
        Assert.Equal(original.ClassicCalibration.GetCalibrationString(), restored.ClassicCalibration.GetCalibrationString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("-pro:joyL|0|0|0|0|0|0|0|0")]
    [InlineData("-cla:joyL|invalid|invalid")]
    public void EmptyOrInvalidCalibrationKeepsDefaults(string serialized)
    {
        Assert.Equal(new CalibrationStorage().ToString(), new CalibrationStorage(serialized).ToString());
    }
}
