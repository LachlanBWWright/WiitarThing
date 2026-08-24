# WiitarThing macOS Port Plan

## Summary

WiitarThing is currently a Windows-specific desktop application. A macOS port is not a simple target framework change because the app depends on WinUI 3, Windows Bluetooth/HID APIs, Windows virtual controller drivers, Windows startup registration, and Windows installer assets.

The safest approach is to split the project into platform-neutral core libraries plus separate Windows and macOS application hosts. The first technical milestone should be proving macOS Wiimote transport and Clone Hero-compatible output before investing heavily in UI work.

## Current Windows Coupling

### UI

- `WiitarThing/WiitarThing.csproj` targets `net10.0-windows10.0.26100.0`.
- The app uses WinUI 3 through `Microsoft.WindowsAppSDK`.
- XAML views, dialogs, title bar behavior, tray behavior, and file dialogs are Windows-specific.

### Device Discovery and Transport

- `Shared/Shared.projitems` imports Windows-only files:
  - `Shared/Windows/DeviceListener.cs`
  - `Shared/Windows/NativeImports.cs`
  - `Shared/Windows/WinBtStream.cs`
- `WinBtStream` uses Windows APIs including:
  - `kernel32.dll` / `CreateFile`
  - `setupapi.dll`
  - `hid.dll`
  - `irprops.cpl` / `bthprops.cpl`
  - `user32.dll` device notifications
- `WiitarThing/Services/DeviceDiscoveryService.cs` calls `WinBtStream.TryGetPaths()` directly.
- `WiitarThing/Windows/MainWindow.xaml.cs` constructs `WinBtStream` directly when refreshing devices.

### Virtual Controller Output

The current virtual output backends are Windows-only:

- SCP Xbox 360 virtual bus
- HIDMaestro
- vJoy
- XInput readback via `xinput1_4.dll` / `xinput9_1_0.dll`

`WiitarThing/VirtualOutput/VirtualControllerBackendFactory.cs` currently creates only these Windows backends.

### Preferences and App Services

Several app services assume Windows behavior:

- Startup registration through the Windows registry and startup shortcuts.
- Tray icon behavior.
- Windows controller test panel launching.
- Single-instance and foreground-window behavior.
- Driver installation prompts for SCP, vJoy, and HIDMaestro.

## Target Architecture

### Core Projects

Create or reshape the solution around these projects:

- `WiitarThing.Core`
  - Mapping logic
  - Calibration logic
  - Preferences models
  - Controller output state models
  - Platform-neutral service contracts
  - Result/error types currently shared across the app

- `Nintroller.Core`
  - Wiimote protocol logic
  - Extension detection
  - Report parsing
  - Controller state models
  - LED, rumble, status, and report-mode command generation

- `WiitarThing.Windows`
  - Existing WinUI 3 app
  - Existing Windows device discovery and transport
  - Existing SCP, HIDMaestro, vJoy, and XInput backends

- `WiitarThing.Mac`
  - New macOS app host
  - macOS device discovery and transport
  - macOS output backend
  - macOS packaging, signing, and startup integration

## Platform Abstractions

Introduce platform-neutral contracts before adding macOS-specific implementations.

Recommended interfaces:

```csharp
public interface IControllerDeviceDiscovery
{
    Result<IReadOnlyList<ControllerDeviceInfo>, DeviceDiscoveryError> DiscoverDevices();
}

public interface IControllerTransportFactory
{
    Result<Stream, HidStreamError> Open(ControllerDeviceInfo device);
}

public interface IDeviceNotificationService
{
    event Action DevicesChanged;
    void Start();
    void Stop();
}

public interface IStartupRegistrationService
{
    Result<Unit, PreferencesError> SetEnabled(bool enabled);
}
```

Keep `IVirtualControllerBackend` as the main output abstraction, but make backend selection platform-aware.

## macOS Technical Spikes

### 1. Wiimote Discovery and HID Transport

This is the highest-risk area. Before building a full UI, create a small console prototype that proves:

- A Wii Remote can be discovered on macOS.
- Input and output HID reports can be opened.
- Status, LED, rumble, and report-mode commands work.
- Extension data can be read from guitars and drums.
- Input latency and packet rate are acceptable for rhythm games.

Implementation options to evaluate:

- HIDAPI through a .NET wrapper.
- Direct IOKit interop.
- CoreBluetooth only if HID access through IOKit/HIDAPI is insufficient.

Deliverable: a console app that prints parsed guitar/drum state at runtime.

### 2. Clone Hero-Compatible Output

Windows output backends do not carry over to macOS. Evaluate output options in this order:

1. Keyboard mapping or direct input events if Clone Hero handles them well.
2. User-space virtual HID options if available and stable.
3. A custom DriverKit virtual HID driver only if necessary.

Avoid starting with a custom DriverKit driver unless the simpler output paths fail. DriverKit adds signing, entitlements, notarization, install prompts, and long-term maintenance burden.

Deliverable: a minimal backend that maps Wiitar input into a usable Clone Hero control path on macOS.

## UI Plan

Use Avalonia for the macOS UI unless there is a strong reason to build a native Swift app.

Reasons:

- Good fit for a C# codebase.
- Allows reuse of ViewModels after platform dependencies are removed.
- Supports macOS packaging.
- Lower rewrite cost than Swift.

Initial macOS UI scope:

- Device list
- Connect/disconnect
- Live input preview
- Calibration
- Mapping selection
- Output backend status
- Error and permission prompts

Do not port Windows driver install prompts to macOS. Replace them with macOS-specific setup/status messages.

## Milestones

### Milestone 1: Separate Core Code

- Create `WiitarThing.Core`.
- Move platform-neutral result/error/model/mapping/calibration code into it.
- Keep Windows app behavior unchanged.
- Add tests for moved code.

### Milestone 2: Make Nintroller Platform-Neutral

- Retarget `Nintroller` from `net8.0-windows` to a neutral TFM such as `net8.0` or `net10.0`.
- Remove `UseWindowsForms`.
- Replace WPF/Forms primitive usage with neutral structs or app-local types.
- Keep Wiimote protocol behavior covered by tests.

### Milestone 3: Abstract Discovery and Transport

- Replace direct `WinBtStream` construction with `IControllerTransportFactory`.
- Replace direct `WinBtStream.TryGetPaths()` calls with `IControllerDeviceDiscovery`.
- Keep `WinBtStream` as the Windows implementation.
- Move `Shared.Windows` code into a Windows-specific project or folder included only by the Windows host.

### Milestone 4: macOS Transport Prototype

- Build a console app for macOS HID discovery and report I/O.
- Confirm real Wii Remote, guitar, and drum behavior.
- Document pairing requirements and macOS permissions.
- Decide whether HIDAPI, IOKit, or another transport layer is viable.

### Milestone 5: macOS Output Prototype

- Implement the smallest Clone Hero-compatible output path.
- Start with keyboard/direct input mapping.
- Measure latency and dropped inputs.
- Only investigate virtual HID driver work if required.

### Milestone 6: macOS App Host

- Create an Avalonia macOS host.
- Reuse neutral ViewModels where practical.
- Implement macOS-specific services for startup, device notifications, file paths, and app lifecycle.
- Add platform-aware backend selection.

### Milestone 7: Packaging and Release

- Create `.app` packaging.
- Add signing and notarization steps.
- Document macOS pairing/setup.
- Add smoke tests for startup, discovery, connection, calibration, output, and shutdown.

## Main Risks

### macOS Wiimote Access

The largest unknown is whether current macOS versions allow reliable user-space access to Wii Remote HID input and output reports without a custom driver.

### Virtual Controller Output

There is no direct macOS equivalent to SCP, vJoy, XInput, or HIDMaestro. The output strategy may determine whether the port is small, medium, or very large.

### Hardware Variability

Official Wii Remotes, third-party Wii Remotes, guitars, drums, DolphinBar, and Bluetooth adapters may behave differently. Hardware test coverage matters.

### Packaging and Permissions

If the app needs low-level input monitoring, HID access, helper tools, or DriverKit, macOS permissions and notarization become part of the product work.

## Recommended First Step

Start with the macOS transport prototype, not the UI.

If the prototype can reliably discover a Wii Remote, exchange HID reports, read extension state, and maintain rhythm-game latency, the rest of the port is straightforward engineering. If it cannot, the project needs a different device access strategy before any UI porting work is useful.
