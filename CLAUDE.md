# CLAUDE.md

Guidance for AI agents (Claude Code and similar) working in this repository.
For a user-facing feature/setup overview, see [README.md](README.md).

## What this is

An **unofficial [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview) (CmdPal) extension for [Redmine](https://www.redmine.org/)**.
It lets you open issues by number, copy rich `#id title` links, keep a recent-ticket
history, browse comments, and run saved filter queries — all from the palette.

- **Language/runtime:** C# on **.NET 10**, `WinExe`, targeting `net10.0-windows10.0.26100.0`.
- **Packaging:** MSIX single-project; the extension runs **out-of-process as a COM server**
  (`Program.cs`, `[MTAThread]`, hosted by `Shmuelie.WinRTServer`). Do not change the hosting pattern.
- **Entry point:** `RedmineExtensionCommandsProvider : CommandProvider` builds the top-level commands.
- **UI framework:** the `Microsoft.CommandPalette.Extensions` toolkit (`ListPage`, `DynamicListPage`,
  `ContentPage`, `ListItem`, `CommandContextItem`, `FormContent`/Adaptive Cards, `Settings`).

## Build, deploy, run

```sh
dotnet build RedmineExtension.sln -c Debug -p:Platform=x64
```

- `dotnet build` verifies compilation but **cannot register or run** the extension.
  Registration/execution requires **Visual Studio → Build → Deploy** (installs the MSIX),
  then the **Reload** command inside Command Palette to pick up the new build.
- There is no automated test suite; verification is manual in the palette.
- The agent environment usually cannot deploy or observe the running palette. When a change
  needs the live palette to verify (navigation, focus, shortcuts), **say so and ask the user to test**
  rather than claiming it works.

## Hard rules

These are non-negotiable; violating them breaks the build gate or the security model.

1. **Stay warning-free.** Debug builds enable the trim/AOT/single-file analyzers and
   `ILLinkTreatWarningsAsErrors`. Any new analyzer warning (CA1305 culture, CA1822 static,
   CA1859 concrete return types, CS86xx nullability, IL trim/AOT) fails the build. Fix, don't suppress.
2. **AOT- and trim-safe only.** `PublishTrimmed`/AOT are on in Release. **No reflection-based
   serialization.** Use `System.Text.Json` **source-generated** `JsonSerializerContext`
   (see `SavedQueryJsonContext`, `HistoryJsonContext`) and `JsonDocument` for parsing.
   P/Invoke uses `[LibraryImport]` (`partial`), not `[DllImport]`.
3. **Never store or log the API key in plaintext.** It lives **only in Windows Credential Manager**
   (`CredentialStore`, generic credential target `RedmineExtension/ApiKey`, DPAPI per-user).
   It is sent only via the `X-Redmine-API-Key` header. Persisted JSON files
   (`saved-queries.json`, `ticket-history.json`) must contain **no secrets**.
4. **Strip credentials from pasted queries.** When a user pastes a raw query/URL for a saved query,
   drop any `key=` parameter (`RedmineApi.NormalizeRawQuery`). Prefer/recommend `https://` because
   the key header is cleartext over `http://`.

## Conventions

- **UI strings live in `Strings.cs`, never inline.** Every user-visible string goes through the
  `Strings` catalog (grouped by feature: Common / Setup / Tickets / Comments / Queries /
  QuickEdit / Fields / SettingsUi) and is bilingual via `L10n.T(ja, en)` — Japanese when the
  Windows display language is Japanese, English otherwise (resolved once at process start; no
  resw/PRI, keeping AOT/trim safety). Inline code comments stay Japanese; identifiers, types,
  and this file stay English.
- **Keyboard shortcuts live in `Keybindings.cs`, never inline.** Pages must not call
  `KeyChordHelpers.FromModifiers` directly. The catalog is grouped (navigation / ticket
  actions / saved-query management) and documents reserved keys; `Keybindings.Back` is
  settings-driven (`Keybindings.Configure(settings)` is called once by the provider).
- **Segoe Fluent icon glyphs — use the marker workaround.** Icon glyphs are private-use codepoints
  (e.g. `U+E90A`) that the file-editing tools tend to mangle. The established pattern:
  write `new IconInfo("")` followed by a `// glyph:XXXX` marker (any `;`/`,` in between is fine),
  then insert the real character with PowerShell. This regex version fills **all** markers in a file
  regardless of the separator — prefer it over a literal `.Replace`, which silently misses
  `IconInfo("");` / `IconInfo(""),` variants:
  ```powershell
  $ev = { param($m) 'IconInfo("' + [string][char][Convert]::ToInt32($m.Groups['code'].Value,16) +
         '")' + $m.Groups['sep'].Value + ' // glyph:' + $m.Groups['code'].Value }
  $raw = Get-Content $f -Raw -Encoding utf8
  [regex]::Replace($raw, 'IconInfo\(""\)(?<sep>[;,]?)\s*// glyph:(?<code>[0-9A-Fa-f]{4})', $ev) |
    Set-Content $f -NoNewline -Encoding utf8
  ```
  After editing, verify no empty markers remain anywhere:
  ```powershell
  Get-ChildItem RedmineExtension -Recurse -Filter *.cs |
    Where-Object FullName -notmatch '\\(bin|obj)\\' | Select-String 'IconInfo\(""\)'
  ```
  (no output = OK).
- **Be gentle on the Redmine server.** One static `HttpClient` (10s timeout). Cache and throttle:
  TTL caches (saved-query counts ~30 min, refreshed oldest-first with ~500 ms spacing), batch
  multi-ticket fetches via `issue_id=1,2,3`, a per-session comments cache, paged result
  fetches (`SavedQueryPage`: `LoadMore()`/`HasMoreItems`, 100 issues per request via
  `offset`), and a session-wide statuses cache (`ChangeStatusPage`: task-deduped, prefetched
  once when ticket contexts are built so the page opens instantly). Prefer adding to an
  existing cache over issuing new round-trips.
- **Settings UI matches the value shape.** Bounded numeric options (history count, count TTL,
  back key) use `ChoiceSetSetting` combos — don't replace them with free-text. Detail-pane
  fields are per-field `ToggleSetting`s keyed `detailField_<key>` from `TicketDetails.Fields`;
  per-query overrides live in `SavedQuery.DetailFields` (`null` = follow the global default),
  edited via a multi-select `Input.ChoiceSet` in `SavedQueryForm`.
- **Two store events with different costs.** `SavedQueryStore.Changed` (add/edit/remove/pin)
  rebuilds top-level commands and hub items; `SavedQueryStore.CountChanged` (fired by
  `UpdateCount`) must only patch item titles in place — never trigger a rebuild from it,
  or list focus resets and pinned pages lose state.
- **Navigation to a page** is done by using the page as an `ICommand` (`new ListItem(page)` /
  `new CommandContextItem(page)`); shortcuts use
  `KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, VirtualKey, scanCode)` (**6 args**).
- **Unified keybinding scheme** (defined in `Keybindings.Actions`; every action is
  user-remappable from the customize form, stored in `ui-config.json`): **Enter** = navigate to
  a page (ticket → comments page, query → result list; leaf items such as comment rows may open
  the browser instead). Defaults: **Ctrl+Enter** = open in browser, **Ctrl+C** = copy rich link,
  **Ctrl+R** = refresh (ticket / query count), **Ctrl+L** = load the next result page,
  **Ctrl+N** = add saved query, **Ctrl+E** = edit, **Ctrl+Delete** = delete,
  **Ctrl+O** = toggle comment order, **Ctrl+S** = change ticket status, **Ctrl+M** = add a
  comment, **Alt+Left** = back, **Alt+Home** = go home (escape deep hierarchies in one step)
  (`Navigation.BackContext()`/`HomeContext()` / trailing `BackItem()`; Esc may be bound to
  "close palette" in CmdPal settings, so keep an explicit path back).
  New actions must be added to `Keybindings.Actions` (id + default + label) so they appear in
  the customize form automatically. `Keybindings.TryParse` requires Ctrl/Alt/Win and accepts
  C+/A+/S+/W+ abbreviations (positional: only the last token is the key) — Ctrl+A and bare
  keys are reserved by the search box. Browser-opening commands are uniformly named
  「ブラウザで開く」. List-item titles never embed symbols (＋/←) — the icon carries that.
- **Error and setup states must be recoverable in place.** Never latch a page on an error or
  "setup required" item: pages that cache built items must rebuild when `IsConfigured` flips
  (see `SavedQueryHubPage._showedSetupPrompt`), failed loads must retry on the next
  `GetItems` (see `ChangeStatusPage._failed`), and error items should offer Enter=再試行
  (`Strings.Common.Retry`) plus back/home contexts so the user is never stranded.
- **Status messages must be transient.** Route completion feedback through
  `BackgroundJob.Run`/`Notify` — they auto-hide (success ~4 s, error ~10 s). Never call
  `ExtensionHost.ShowStatus` directly without a matching timed `HideStatus`, or the message
  stays on screen indefinitely.
- **Never block `Invoke()` / `SubmitForm()` on network I/O.** They run on the COM response
  thread; a synchronous wait (`GetAwaiter().GetResult()`) freezes the whole palette for up to
  the HttpClient timeout. Route server calls through `BackgroundJob.Run` (progress + result via
  `ExtensionHost.ShowStatus`) and return `GoBack`/`KeepOpen` immediately.
- **Two settings surfaces.** The CmdPal settings page (`SettingsManager`) holds connection
  essentials only (URL / API key / history count / count TTL) so the first-run prompt stays
  lean. Everything cosmetic or behavioral (default detail-pane fields, pin-new default, all
  keybindings) lives in the customize form (`CustomizeForm` → `UiConfigStore`,
  `ui-config.json`). Don't add settings to the settings page unless they're required to connect.
- **Keep git history linear.** Work on a feature branch, squash into a coherent commit
  (`git merge --squash` or `git reset --soft main`), then fast-forward into `main`. Avoid merge branches.

## Code map (`RedmineExtension/`)

Folders are physical organization only — everything stays in the single `RedmineExtension`
namespace. `Api/` REST client · `Commands/` invokable commands & shared contexts ·
`Pages/` list/content pages & forms · `Models/` persisted models & stores ·
`Settings/` connection settings & UI customization store · `UI/` string/keybinding
catalogs & presentation helpers · `Infra/` Win32 interop.

| Area | Files |
|------|-------|
| COM server / extension shell | `Program.cs`, `RedmineExtension.cs`, `Package.appxmanifest`, `app.manifest` |
| Top-level commands | `RedmineExtensionCommandsProvider.cs` |
| Redmine REST client | `Api/RedmineApi.cs` (issues, threads, search, batch, update, statuses) |
| Number search + recent history | `Pages/RedmineExtensionPage.cs`, `Models/TicketHistory.cs` |
| Ticket actions | `Commands/OpenTicketCommand.cs`, `Commands/CopyTicketLinkCommand.cs`, `Commands/BackgroundJob.cs` (non-blocking server calls), `Infra/RichLinkClipboard.cs` (CF_HTML via Win32) |
| Quick edit / navigation | `Pages/ChangeStatusPage.cs` (+`SetStatusCommand`), `Pages/AddCommentPage.cs`, `Commands/TicketEdit.cs` (shared Ctrl+S/Ctrl+M contexts), `Commands/Navigation.cs` (back) |
| Catalogs (single source of truth) | `UI/Strings.cs` (all UI strings, bilingual), `UI/Keybindings.cs` (all shortcuts + parser), `UI/L10n.cs` |
| Right-pane details / query text | `UI/TicketDetails.cs`, `UI/SavedQueryText.cs` |
| Comments | `Pages/CommentsPage.cs` |
| Saved queries | `Models/SavedQuery.cs` (model + `SavedQueryStore`), `Pages/SavedQueryForm.cs`, `Pages/SavedQueryPage.cs`, `Pages/SavedQueryHubPage.cs` |
| Settings & customization | `Settings/SettingsManager.cs` (connection), `Settings/UiConfig.cs` (`UiConfigStore`), `Pages/CustomizeForm.cs` (+`CustomizePage`), `Infra/CredentialStore.cs` |

Persisted state lives in the MSIX **LocalState** folder (`saved-queries.json`,
`ticket-history.json`, `ui-config.json`); if `LocalState` is unavailable (unpackaged run),
state degrades to in-memory only.

## Known issues

- **Context-command pages need Esc/back twice** to return — **confirmed in the live palette**.
  CmdPal pushes an extra navigation frame when a page is opened via a *context* command
  (primary/Enter navigation does not; moving the comments page to Enter fixed it there).
  The quick-edit pages (**Ctrl+S** → `ChangeStatusPage`, **Ctrl+M** → `AddCommentPage`) are
  affected. The back command (`GoBack`) pops one frame per press. A candidate fix to try:
  explicit `CommandResult.GoToPage(NavigationMode.Push)` from an `AnonymousCommand` instead of
  using the page as the context command.

## Publishing

- **CmdPal only discovers MSIX-packaged extensions** — enumeration is
  `AppExtensionCatalog.Open("com.microsoft.commandpalette")` in the host's
  `WinRTExtensionService`; there is **no registry-based discovery of unpackaged extensions**
  (verified 2026-07 against the PowerToys sources and the installed 0.11 binary). The
  unpackaged-EXE recipe in `.github/skills/publish-extension/references/winget-publishing.md`
  comes from the official extension template but **does not match the product — do not trust
  it for distribution decisions**. Public distribution therefore requires a signed MSIX
  (Microsoft Store, or winget-pkgs with `InstallerType: msix`); see `RELEASING.md`.
- The EXE pipeline (`build-exe.ps1` + `setup.iss` + release.yml) still builds and installs
  correctly but the result is invisible to CmdPal; it is kept only as a record / for a
  possible future host change. `-p:WindowsPackageType=None` flips the csproj to a
  framework-dependent, non-trimmed publish (no ILLink → robust across SDK installs).
- CI (`build.yml`) runs the warning-free Debug build on every push/PR; keep it green.

## More docs

- `.github/copilot-instructions.md` — short CmdPal build/deploy notes and skill index.
- `.github/skills/` — task recipes (Adaptive Card forms, settings, dock bands, fallback commands, publishing).
- [Creating a CmdPal extension](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension) ·
  [Extensibility overview](https://learn.microsoft.com/windows/powertoys/command-palette/extensibility-overview)
