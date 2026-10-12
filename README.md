# EnhancedMap

A free Ultima Online mapping tool. This repository has been ported to **.NET 10** with a
cross-platform **Avalonia** UI and **SkiaSharp** rendering (replacing the original WinForms/GDI+).

## Projects

| Project | Description |
| --- | --- |
| `src/EnhancedMap.Core` | Platform-independent core: networking/protocol, settings, map model, and the SkiaSharp map renderer/generator. |
| `src/EnhancedMap.Desktop` | Cross-platform Avalonia desktop app (main map window, chat, settings, Map Maker, tools). Runs standalone. |
| `EnhancedMapServerNetCore` | The server that EnhancedMap clients connect to. |

## Build & run

Requires the .NET 10 SDK.

```sh
# build everything
dotnet build EnhancedMap.slnx

# run the desktop app
dotnet run --project src/EnhancedMap.Desktop
```

On first run (with no maps present) the Map Maker opens so you can generate maps from your
Ultima Online client data (MUL / `mapNLegacyMUL.uop`) or download a prebuilt set.

## Map generation

Maps are produced from the UO client directory (`radarcol.mul`, `mapN.mul` / `mapNLegacyMUL.uop`,
`staidxN.mul`, `staticsN.mul`) and written to `Maps/` as PNGs. DDS (`facet0N.dds`) sources are not
supported yet.

## TazUO integration

The former TazUO plugin has been removed. EnhancedMap support is intended to be built
directly into TazUO; see [`TazUO-EnhancedMap-Integration.md`](TazUO-EnhancedMap-Integration.md)
for the protocol reference and a step-by-step implementation guide.

## EnhancedMap server

```sh
cd EnhancedMapServerNetCore
dotnet publish -c Release
```
