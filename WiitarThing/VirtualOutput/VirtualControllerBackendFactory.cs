namespace WiinUSoft.VirtualOutput;

internal static class VirtualControllerBackendFactory
{
    public static IVirtualControllerBackend Create(VirtualOutputMode mode) => mode switch
    {
        VirtualOutputMode.ScpXbox360 => new ScpXInputBackend(),
        VirtualOutputMode.HidMaestroExperimental => new HidMaestroBackend(),
        VirtualOutputMode.VJoyExperimental => new VJoyBackend(),
        _ => new ScpXInputBackend()
    };
}
