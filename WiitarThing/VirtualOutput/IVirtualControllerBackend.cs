using NintrollerLib;
using Shared;

namespace WiinUSoft.VirtualOutput;

internal interface IVirtualControllerBackend : System.IDisposable
{
    string DisplayName { get; }
    VirtualOutputMode Mode { get; }
    Result<Unit, VirtualControllerError> Connect(int slotOrDeviceId, ControllerType sourceType);
    Result<Unit, VirtualControllerError> Update(ControllerOutputState state);
    Result<Unit, VirtualControllerError> Disconnect();
    Result<VirtualControllerIdentity, VirtualControllerError> GetIdentity();
    Result<IVirtualControllerReadback, VirtualControllerError> CreateReadback();
}
