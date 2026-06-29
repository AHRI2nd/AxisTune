# AxisTune

A Windows app that reads any controller (Xbox / PlayStation / Switch Joy-Con and more), **refines**
stick & trigger input with adjustable input ranges, deadzones, and response curves, then outputs a
clean **virtual Xbox 360 controller** — so games only ever see the processed input.

Built with **.NET 10 + Avalonia**, **SDL3** (input), and **ViGEmBus + HidHide** (virtual output &
device hiding).

## Features

- **Auto-detection** of Xbox / PlayStation / Switch Pro / Joy-Con controllers.
- **Manual mapping** for unrecognized controllers — bind any physical button/axis to an Xbox control
  by pressing it ("press to bind").
- **Per-axis tuning**: input min/max, inner/outer deadzone, axis invert, and a draggable **response
  curve editor** with live preview.
- **Multiple named profiles** — create, rename, delete, and switch instantly.
- **Driver On/Off** toggle in the app and the always-visible **tray icon**.
- **Background operation**: closing the window minimizes to tray; tray → Exit fully quits and
  restores drivers.
- **Run at startup** (optional), **rumble pass-through** (game → virtual → physical).
- **In-app driver installer** for ViGEmBus / HidHide, with download-page fallback.
- **English / Korean** UI.

## Requirements

- Windows 10/11 (x64)
- **.NET 10 Desktop Runtime** (or SDK to build)
- **ViGEmBus** and **HidHide** drivers — installable from the app's **Drivers** tab, or see
  [drivers/README.md](drivers/README.md).
- Administrator rights (required by the drivers; the app requests elevation automatically).

## Run

```powershell
dotnet build -c Release
# Launch the built exe as Administrator:
Start-Process "AxisTune.App\bin\Release\net10.0-windows\AxisTune.exe" -Verb RunAs
```
> `dotnet run` fails because the app manifest requires elevation — run the built `AxisTune.exe`
> as Administrator instead. Pass `--minimized` to start hidden in the tray.

## Test

```powershell
dotnet test
```
Then verify end-to-end with a gamepad tester (e.g. https://hardwaretester.com/gamepad or `joy.cpl`):
the virtual Xbox 360 pad should appear and mirror your input, while the physical device is hidden.

## Project structure

```
AxisTune.Core    Processing pipeline · response curves (LUT) · axis math · mapping · profiles  (UI/driver-free, tested)
AxisTune.Input   SDL3 device enumeration/classification · polling · raw joystick · rumble out
AxisTune.Output  ViGEm virtual Xbox 360 pad · HidHide hiding/whitelist
AxisTune.App     Avalonia UI · tray · engine (hot loop) · settings · localization
AxisTune.Core.Tests   xUnit unit tests
```

## Limitations

- **XInput controllers can't be hidden.** HidHide only hides HID/DirectInput devices. A controller
  that presents itself as an Xbox/XInput device cannot be hidden from XInput games, so its raw input
  may still reach them (causing double input). Workaround: switch the controller to **DInput mode**
  (then it's a HID device and hiding works), or use a per-game XInput proxy. This is an inherent
  HidHide limitation — reWASD avoids it with its own (signed) kernel driver.

## License / drivers

ViGEmBus and HidHide are open-source drivers by Nefarius and are governed by their own licenses.
