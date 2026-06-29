# AxisTune — Roadmap / Backlog

Planned improvements, grouped by area. Not yet scheduled — captured for later.

## Robustness (bug-risk — high priority)
- **Hotplug handling** — subscribe to SDL `GAMEPAD_ADDED/REMOVED`; auto-refresh the device list,
  stop output when the selected device disconnects, and re-acquire on reconnect.
- ~~**Single-instance lock**~~ — done (named Mutex + show-signal in `Program.cs`).
- **Engine loop exception guard** — wrap the main loop body (read/process/submit) so a transient
  exception can't kill the engine thread.
- **HidHide targeting hardening** — also hide sibling nodes for Bluetooth/composite devices.

## Core processing quality
- **Radial (vector) deadzone** for sticks — better diagonal accuracy than per-axis deadzones.
- **Remap layer for recognized gamepads** — swap buttons/sticks without dropping the auto-mapping
  (currently manual mapping forces raw-joystick mode).
- **More transforms** — trigger threshold / hair-trigger, anti-deadzone, sensitivity, gyro→stick
  (SDL exposes gyro for DualSense/Switch), turbo/toggle.
- **DualShock4 virtual output** option (ViGEm supports it) for games preferring the PS layout.

## UX
- Curve editor: numeric point entry, grid snap, reset, copy between axes (link X/Y).
- Tray quick-profile-switch submenu; in-app input tester (live state) so no external site is needed.
- Profile delete confirmation; import/export (share JSON); per-device auto-profile binding.
- First-run onboarding / driver-check wizard; toast on driver on/off.
- Persist window **position** too (currently size only) with off-screen guard.

## Supportability / code quality
- ~~**Crash logging**~~ — basic global handler added (`%APPDATA%/AxisTune/crash.log`). Extend to a
  general diagnostics log for driver/device failures (currently many are silently caught).
- **Testability via interfaces** — abstract SDL/ViGEm (`ISdlService`, `IVirtualController`) so the
  engine (mode switch, capture, rumble bridge) can be unit-tested without hardware.
- More tests: ProfileDocument JSON file round-trip, mapping edge cases, debounced save.

## Distribution / CI
- **App code signing** (standard cert, not EV) to reduce SmartScreen "unknown publisher" warnings
  and speed up first launch.
- **Verify Authenticode signature** of the downloaded driver installer before running it.
- CI: derive version from a git tag, auto-create a GitHub Release with the installer attached,
  also emit a portable zip, cache NuGet, and add a lightweight build+test gate on push.
