# Building SIDERAL from source

This repository contains the SIDERAL application source code and mod configuration data.

It intentionally does not include Elden Ring game files, generated build outputs, Nexus release archives, Mod Engine 2 binaries, or local third-party binary dependencies.

## Requirements

- Windows x64.
- .NET 9 SDK.
- Elden Ring installed locally if you want to run the generator.
- Optional for the Standalone launch profile: Mod Engine 2, downloaded separately from:
  https://github.com/soulsmods/ModEngine2/releases

## Required local files not committed to Git

Create a `lib` folder at the repository root and place the binary dependencies used by the project there.

Expected files:

- `lib/Andre.SoulsFormats.dll`
- `lib/DotNext.Unsafe.dll`
- `lib/DotNext.dll`
- `lib/ZstdSharp.dll`
- `lib/ZstdNet.dll`
- `lib/BouncyCastle.Cryptography.dll`
- `lib/CommunityToolkit.HighPerformance.dll`
- `lib/libzstd.dll`
- `lib/oo2core_6_win64.dll`
- `lib/oo2core_9_win64.dll`

The two `oo2core` DLLs are used by SoulsFormats/Oodle handling for Elden Ring DCX/msgbnd files. They can be obtained from a local Elden Ring installation.

To run the generator, also create the expected `Base` layout locally:

- `Base/regulation_base.bin`
- `Base/msg/...` when msgbnd text generation is needed

These files are intentionally not committed because they are game files.

## Build

From the repository root:

```powershell
dotnet restore RandomMagicConversion.csproj
dotnet build RandomMagicConversion.csproj -c Release
```

## Publish

```powershell
dotnet publish RandomMagicConversion.csproj -c Release -o "bin/Publish"
```

The publish output contains the application files that are packaged for Nexus Mods.

## Quick version check

```powershell
dotnet "bin/Publish/SIDERAL.dll" --version
```

Expected for the current source state:

```text
SIDERAL V1.0.3
Release date: 2026-07-14
```

## Runtime notes

- `Compatible Randomizer` profile generates `Output/regulation.bin` for use with the Elden Ring Randomizer merge workflow.
- `Standalone` profile can launch through Mod Engine 2, but Mod Engine 2 is downloaded separately by the user.
- The application can ask the user to select `modengine2_launcher.exe` manually.

