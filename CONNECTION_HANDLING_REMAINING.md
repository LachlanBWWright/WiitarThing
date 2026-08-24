# Connection Handling: Remaining Work

This document records the Bluetooth, HID, and controller-transport issues that remain after the low-risk hardening pass on the `connection-handling` branch.

## Addressed in the current pass

- HID discovery handles are now scoped with `using`, including invalid-handle checks.
- Repeated `Nintroller.BeginReading` calls are serialized so they cannot start overlapping read chains.
- Read watchdog wait handles are disposed and no longer send a status request after reading has stopped.
- HID buffer ranges are validated before reads and writes; non-zero write failures now reach the controller disconnect path.
- HID open attempts have one bounded shared-access retry and emit debug diagnostics for attempts and failures.
- Pairing radio/device search handles are closed in `finally` blocks, and the managed inquiry loop responds to cancellation.
- Duplicate disconnect notifications are suppressed for a connection session.
- Fake transport tests cover overlapping-read prevention and duplicate disconnect protection.
- Existing build, test, exception-policy, and UI-gallery checks remain passing.

## Not yet addressed

### 1. Replace the callback read chain with a cancellable read loop

`Nintroller` still uses `BeginRead`/`EndRead` callbacks and creates a watchdog task for each read. `StopReading` stops scheduling new reads but does not cancel or await an already pending native read.

This needs testing with a controller disconnected during an in-flight read before changing it. The preferred design is one serialized read loop with explicit cancellation and a defined shutdown await point.

### 2. Validate report framing and partial reads

The callback currently ignores the byte count returned by `EndRead` and passes the entire fixed-size buffer to the parser. A short read can therefore leave stale bytes in the buffer.

The transport should accumulate bytes until a complete HID report is available, reject zero-byte reads as disconnects, and validate report lengths before parsing.

### 3. Complete write-failure propagation across all device layers

The primary `WinBtStream`/`Nintroller` path now marks the controller disconnected and raises one canonical event. Other device-control layers still need an audit to ensure they do not swallow or independently re-emit transport failures.

This requires coordinating `WinBtStream`, `Nintroller`, and `DeviceControl` so a single hardware failure cannot produce duplicate disconnect notifications.

### 4. Add bounded reconnect behavior

The application can rediscover devices, but there is no unified reconnect policy for transient HID failures. Reconnect attempts should have bounded retries, backoff, and a clear distinction between a temporarily unavailable device and a removed device.

### 5. Make native pairing cancellation interruptible

The pairing dialog now cancels the managed scan loop and cleans up handles. An active Windows Bluetooth inquiry may still continue until the native timeout expires because the current API call is not directly interruptible.

Pairing should use bounded inquiry phases and guarantee radio/device handle cleanup on every exit path.

### 6. Preserve and restore Bluetooth adapter state

Pairing enables discovery and incoming connections through the Windows Bluetooth APIs. The previous adapter state is not currently captured and restored after pairing completes or is cancelled.

This needs a deliberate UX decision because changing adapter state can affect other applications.

### 7. Consolidate mutable Bluetooth/HID configuration

`WinBtStream` uses process-wide mutable settings for Toshiba mode, sharing mode, and report-size behavior. These settings can make concurrent connections influence one another.

They should eventually become immutable per-connection options, with compatibility defaults retained at the construction boundary.

### 8. Centralize controller identification and protocol validation

Device matching still depends on hard-coded Nintendo VID/PID values and Bluetooth names. Report parsing also relies on many fixed offsets and controller-specific assumptions.

These should be centralized into versioned identification/protocol definitions with explicit validation and diagnostic logging.

### 9. Expand hardware-independent transport tests

Initial fake-stream coverage is now present. Expand it with recorded report fixtures for:

- partial and zero-byte reads;
- disconnect during a read;
- write failures;
- repeated start/stop calls;
- malformed and unknown reports;
- extension attach/detach sequences;
- duplicate disconnect prevention.

### 10. Validate against physical hardware

The remaining lifecycle and framing work should be tested with at least:

- a standard Wiimote;
- a Wii U Pro Controller;
- a Wiimote with a Nunchuk or Classic Controller;
- Microsoft and Toshiba Bluetooth-stack configurations where available.

No changes should be made to report timing or reconnect semantics solely from simulated tests.
