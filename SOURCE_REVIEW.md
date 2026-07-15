# Source review notes

This repository is intended to make the SIDERAL source code reviewable for Nexus Mods moderation.

## Included

- C# source code for the Windows Forms application.
- Conversion pipeline configuration files under `Data`.
- Paramdef XML files under `Defs`.
- UI localization strings.
- Build metadata and changelog.

## Not included

- Elden Ring game files, including `regulation.bin` and `msgbnd` files.
- Generated `Output` files and run logs.
- Nexus release archives.
- Mod Engine 2 binaries.
- Local binary dependencies under `lib`.
- Yabber binaries/tools.

These exclusions are intentional so the repository remains source-focused and does not redistribute game files or unrelated third-party tools.

## Build instructions

See `BUILDING.md`.

