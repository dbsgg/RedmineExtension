---
name: publish-extension
description: >-
  Publish your Command Palette extension to the Microsoft Store or WinGet.
  Use when asked to publish, distribute, release, deploy to store,
  create MSIX packages, submit to WinGet, set up CI/CD for releases,
  or automate builds with GitHub Actions.
---

# Publish Your Command Palette Extension

Guide for distributing your Command Palette extension through the Microsoft Store, WinGet, or both.

## When to Use This Skill

- Publishing your extension to the Microsoft Store
- Submitting your extension to WinGet for `winget install` discovery
- Setting up GitHub Actions to automate builds and releases
- Creating MSIX packages for Store submission
- Creating signed MSIX bundles for WinGet submission

## Publishing Options

| Channel | Package Format | Discovery | Auto-Updates |
|---------|---------------|-----------|--------------|
| Microsoft Store | MSIX bundle | Store app, `ms-windows-store://` link | Yes |
| WinGet | Signed MSIX bundle | `winget install`, CmdPal browse | Yes (via manifest) |

**Recommendation**: Publish to both for maximum reach. WinGet enables direct discovery from within Command Palette.

## Workflows

### Microsoft Store Publishing
See [store-publishing.md](references/store-publishing.md) for the complete step-by-step guide.

**Summary:**
1. Register for Partner Center
2. Update `Package.appxmanifest` and `.csproj` with Partner Center identity
3. Build MSIX for x64 and ARM64
4. Create MSIX bundle
5. Submit to Partner Center

### WinGet Publishing
> ⚠️ The unpackaged-EXE recipe that used to live in `references/winget-publishing.md` (from the
> official extension template) does **not** work: Command Palette only discovers MSIX-packaged
> extensions, so an EXE-installed extension never appears in the palette. WinGet publishing
> requires a **signed MSIX** (`InstallerType: msix`) — see `RELEASING.md` (route B) at the repo root.

**Summary:**
1. Build the MSIX bundle (`build-msix.ps1`) and sign it with your code-signing certificate
2. Attach the signed bundle to a GitHub Release
3. Generate manifests via `wingetcreate new`, add the `windows-commandpalette-extension` tag and the WindowsAppRuntime dependency
4. Run `winget validate`, then submit

## Prerequisites

- [Visual Studio](https://visualstudio.microsoft.com/) with C# and WinUI workloads
- [Partner Center account](https://partner.microsoft.com/dashboard/home) (for Store publishing)
- [GitHub CLI](https://cli.github.com/) (for WinGet publishing)
- [WingetCreate](https://github.com/microsoft/winget-create) — `winget install Microsoft.WingetCreate`
- A code-signing certificate (for the WinGet route; the Store signs for you)

## Important Notes

- Your extension's CLSID (the `[Guid("...")]` in your main .cs file) must be unique and consistent across all files
- WinGet manifests must include the `windows-commandpalette-extension` tag for CmdPal discovery
- MSIX packages require both x64 and ARM64 builds for Store submission
- WindowsAppSdk must be listed as a dependency in WinGet manifests
