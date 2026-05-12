using Shared;

namespace WiinUSoft.VirtualOutput;

internal interface IVirtualControllerReadback : System.IDisposable
{
    string DisplayName { get; }
    Result<Unit, VirtualControllerError> Attach(VirtualControllerIdentity identity);
    Result<ControllerOutputState, VirtualControllerError> ReadState();
}
