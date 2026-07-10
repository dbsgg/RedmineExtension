# Releasing / Publishing

Maintainer guide for distributing this Command Palette extension.

Command Palette discovers extensions **only through their MSIX `appExtension` registration**
(`AppExtensionCatalog.Open("com.microsoft.commandpalette")`). An unpackaged EXE installer that
merely registers a COM server in the registry installs fine but is **not** picked up by the
palette. Distribution therefore uses a **signed MSIX** via the Microsoft Store (recommended) or
winget. The EXE pipeline (`build-exe.ps1` / `setup.iss`) is kept only as a build reference.

## Distribution routes

| Route | Cost | Notes |
|---|---|---|
| **A. Microsoft Store (recommended)** | Individual dev account, one-time fee | The Store signs the package for you; also reachable via `winget search -s msstore`. |
| B. winget-pkgs + self-signed/-signed MSIX | Code-signing cert | Host a signed `.msix` on a GitHub Release and submit with `InstallerType: msix`. Also lets you appear in the palette's browse experience via the `windows-commandpalette-extension` tag. |

## Prerequisites

```powershell
# For local MSIX bundling, the Windows SDK (makeappx) is required; it ships with Visual Studio
# or the standalone Windows SDK. build-msix.ps1 auto-detects makeappx.

# For the optional winget route:
winget install Microsoft.WingetCreate
```

Repo settings for the winget automation (optional, route B):

- Actions → Variables: `WINGET_ENABLED = true` (enables the winget-update job in `release.yml`)
- Actions → Secrets: `WINGET_PAT` — a PAT with `public_repo` scope for PRs to `winget-pkgs`

---

# Route A — Microsoft Store

## A-1. Partner Center setup

1. Register a developer account at [Partner Center](https://partner.microsoft.com/dashboard/home).
2. **Apps and Games → New product → MSIX or PWA app** and reserve an app name
   (e.g. `Redmine for Command Palette`).
3. Open **Product management → Product identity** and note these three values:
   - **Package/Identity/Name** (e.g. `Publisher.RedmineForCommandPalette`)
   - **Package/Identity/Publisher** (e.g. `CN=...`)
   - **Package/Properties/PublisherDisplayName**

> Keep these reserved values out of the repo — inject them as `build-msix.ps1` arguments
> (or the CI variables below).

## A-2. Store assets (must be prepared)

A Store listing needs:

- [x] Real app logos — `RedmineExtension/Assets/` holds a unique icon set (deep-red gradient
      with a white `#` glyph; certification rejects template placeholder images)
- [x] At least one screenshot — `screenshots/` (1920x1080, JA/EN)
- [x] A privacy policy URL (required — the extension makes HTTPS calls to Redmine and handles
      an API key): `PRIVACY.md` on GitHub

Listing copy (EN/JP) and certification notes are drafted in `STORE-LISTING.md`.

## A-3. Build the MSIX bundle

The first release is **1.0.0**. The package is unsigned; the Store signs it on submission.

**Recommended — CI (official SDK, trimmed = smaller package):**

- Set Actions → Variables: `STORE_IDENTITY_NAME` and `STORE_PUBLISHER` to the reserved values.
- GitHub → Actions → **Store package** → Run workflow (version = `1.0.0`) and download the
  `store-msixbundle-<version>` artifact (`.github/workflows/store-package.yml`).

**Local:**

```powershell
.\build-msix.ps1 -Version 1.0.0 -IdentityName <reserved-name> -Publisher "<reserved-publisher>"
# → MsixPackages\RedmineExtension_1.0.0.0_Bundle.msixbundle
```

- The script rewrites the manifest Identity/Version only during the build and restores it in a
  `finally` (never commit that change).
- `makeappx.exe` is auto-detected from PATH / the Windows SDK.
- Trimming is on by default (smaller package). If the trimmed build fails on a particular SDK
  install, pass `-NoTrim` (larger package) — this is why CI is the recommended source for the
  final submission artifact.

## A-4. Submit in Partner Center

1. Your app → **Start a new submission**.
2. **Packages** → upload the `.msixbundle`.
3. **Store listings** → paste the description from `STORE-LISTING.md` (state the PowerToys /
   Command Palette prerequisite).
4. **Notes for certification** → include the test steps from `STORE-LISTING.md`.
5. Set pricing / markets and **submit for certification** (typically 1–3 business days).

> A Store-only listing does not appear in the palette's built-in browse experience. Users find
> it via a direct Store link or `ms-windows-store://assoc/?Tags=AppExtension-com.microsoft.commandpalette`.
> To also appear in browse, additionally publish to winget (route B).

## A-5. Updates

Bump the version and repeat A-3…A-4. The Store auto-updates installed users.

---

# Route B — winget (optional)

Requires a **signed** `.msix`/`.msixbundle` (the Store route does not — Partner Center signs).
Build the bundle as in A-3, sign it with your code-signing certificate, attach it to a GitHub
Release, then:

```powershell
wingetcreate new "<url-to-signed.msixbundle>"
```

Before submitting, edit the generated manifests:

1. In every `*.locale.*.yaml`, add the browse tag:
   ```yaml
   Tags:
   - windows-commandpalette-extension
   ```
2. In `*.installer.yaml`, declare the Windows App SDK runtime dependency (match the
   `Microsoft.WindowsAppSDK` version in `Directory.Packages.props`):
   ```yaml
   Dependencies:
     PackageDependencies:
     - PackageIdentifier: Microsoft.WindowsAppRuntime.2.0
   ```
3. Run `winget validate --manifest <dir>` before `--submit` (or open the PR manually).

Subsequent releases can be automated via `release.yml` (tag push → build → GitHub Release →
`wingetcreate update` PR) once `WINGET_ENABLED` and `WINGET_PAT` are set.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Not shown in the palette's browse | Missing `windows-commandpalette-extension` tag in the locale YAML (winget route) |
| Extension missing after install | Confirm it was installed as an MSIX (not the EXE), and restart Command Palette |
| WinRT type-load failure on start | Missing WindowsAppRuntime dependency declaration |
| winget PR validation fails | Re-run `winget validate` locally and fix the reported issues |
| Trimmed MSIX build fails locally | Build with `-NoTrim`, or use the CI workflow (official SDK) |
