namespace WiinUSoft.VirtualOutput;

internal sealed record VirtualControllerIdentity(
    string BackendDisplayName,
    int? XInputSlot = null,
    string? DeviceGuid = null,
    string? HidPath = null,
    ushort? VendorId = null,
    ushort? ProductId = null,
    string? ProductName = null,
    string? InstanceId = null)
{
    public string ToCompactDisplayString()
    {
        if (XInputSlot.HasValue)
            return $"{BackendDisplayName} (slot {XInputSlot.Value})";

        if (!string.IsNullOrWhiteSpace(ProductName))
            return $"{BackendDisplayName} ({ProductName})";

        return BackendDisplayName;
    }
}
