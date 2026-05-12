using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace WiinUSoft.VirtualOutput;

internal static class VirtualOutputNativeLibraryResolver
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
            return;

        NativeLibrary.SetDllImportResolver(typeof(VirtualOutputNativeLibraryResolver).Assembly, ResolveNativeLibrary);
    }

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string? path = null;

        if (libraryName.Equals(VJoyBackend.VJOY_INTERFACE_DLL, StringComparison.OrdinalIgnoreCase))
            path = VJoyBackend.FindVJoyInterfacePath();
        return path != null && NativeLibrary.TryLoad(path, out IntPtr handle)
            ? handle
            : IntPtr.Zero;
    }
}
