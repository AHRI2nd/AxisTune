# AxisTune

A Windows app (reWASD-style) that reads any controller (Xbox / PlayStation / Switch Joy-Con and more),
refines stick & trigger input with adjustable min/max ranges and response curves, and outputs a clean
virtual Xbox 360 controller so games only see the processed input.

Built with .NET 10 + Avalonia, SDL3 (input), and ViGEmBus + HidHide (virtual output & device hiding).
See [CLAUDE.md](CLAUDE.md) for architecture and [drivers/README.md](drivers/README.md) for required drivers.
