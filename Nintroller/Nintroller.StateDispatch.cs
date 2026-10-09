using System;
using System.Diagnostics;

namespace NintrollerLib;

public partial class Nintroller
{
    /// <summary>Dispatches parsed state without coupling the parser branch to subscribers.</summary>
    private void DispatchStateUpdate()
    {
        var args = new NintrollerStateEventArgs(_currentType, _state!, BatteryLevel);
        try
        {
            StateUpdate(null, args);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"State update subscriber failed: {exception}");
        }
    }
}
