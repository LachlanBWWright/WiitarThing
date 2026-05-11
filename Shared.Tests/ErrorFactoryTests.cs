using System;
using Shared;
using Xunit;

namespace Shared.Tests;

public class ErrorFactoryTests
{
    [Fact]
    public void PreferencesErrorFactoriesPopulateFields()
    {
        var ex = new UnauthorizedAccessException("denied");
        var error = PreferencesError.AccessDenied("prefs.config", ex);

        Assert.Equal(PreferencesErrorKind.AccessDenied, error.Kind);
        Assert.Equal("prefs.config", error.Path);
        Assert.Same(ex, error.Exception);
        Assert.Equal(error.Message, error.ToDisplayString());
    }

    [Fact]
    public void HidStreamAndVirtualControllerFactoriesPopulateFields()
    {
        var hid = HidStreamError.WriteFailed("write failed", "hid#1");
        Assert.Equal(HidStreamErrorKind.WriteFailed, hid.Kind);
        Assert.Equal("hid#1", hid.DevicePath);

        var vc = VirtualControllerError.SlotUnavailable(2);
        Assert.Equal(VirtualControllerErrorKind.SlotUnavailable, vc.Kind);
        Assert.Equal(2, vc.RequestedSlot);
    }

    [Fact]
    public void BluetoothAndCalibrationFactoriesPopulateFields()
    {
        var bt = BluetoothError.DcidNotFound("missing dcid");
        Assert.Equal(BluetoothErrorKind.DcidNotFound, bt.Kind);

        var cal = CalibrationError.ValidationFailed("bad cal");
        Assert.Equal(CalibrationErrorKind.ValidationFailed, cal.Kind);
        Assert.Equal("bad cal", cal.ToDisplayString());
    }

    [Fact]
    public void ErrorRecordsSupportValueEquality()
    {
        var first = DeviceDiscoveryError.NoneFound();
        var second = DeviceDiscoveryError.NoneFound();

        Assert.Equal(first, second);
    }
}
