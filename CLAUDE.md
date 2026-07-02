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

- **Japanese UI + comments.** All user-visible strings and most inline comments are in **Japanese**.
  Keep new UI strings and comments consistent with that. Identifiers, types, and this file stay in English.
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
  multi-ticket fetches via `issue_id=1,2,3`, a per-session comments cache, and paged result
  fetches (`SavedQueryPage`: `LoadMore()`/`HasMoreItems`, 100 issues per request via
  `offset`). Prefer adding to an existing cache over issuing new round-trips.
- **Two store events with different costs.** `SavedQueryStore.Changed` (add/edit/remove/pin)
  rebuilds top-level commands and hub items; `SavedQueryStore.CountChanged` (fired by
  `UpdateCount`) must only patch item titles in place — never trigger a rebuild from it,
  or list focus resets and pinned pages lose state.
- **Navigation to a page** is done by using the page as an `ICommand` (`new ListItem(page)` /
  `new CommandContextItem(page)`); shortcuts use
  `KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, VirtualKey, scanCode)` (**6 args**).
- **Unified keybinding scheme.** Keep new commands consistent with this; don't rebind these keys:
  **Enter** = navigate to a page (ticket → comments page, query → result list; leaf items such as
  comment rows may open the browser instead), **Ctrl+Enter** = open in browser,
  **Ctrl+C** = copy rich link, **Ctrl+R** = refresh (ticket / query count),
  **Ctrl+L** = load the next result page, **Ctrl+N** = add saved query,
  **Ctrl+E** = edit, **Ctrl+Delete** = delete.
  Avoid Ctrl+A (conflicts with select-all in the search box). Browser-opening commands are
  uniformly named 「ブラウザで開く」.
- **Keep git history linear.** Work on a feature branch, squash into a coherent commit
  (`git merge --squash` or `git reset --soft main`), then fast-forward into `main`. Avoid merge branches.

## Code map (`RedmineExtension/`)

| Area | Files |
|------|-------|
| COM server / extension shell | `Program.cs`, `RedmineExtension.cs`, `Package.appxmanifest`, `app.manifest` |
| Top-level commands | `RedmineExtensionCommandsProvider.cs` |
| Number search + recent history | `Pages/RedmineExtensionPage.cs`, `TicketHistory.cs` |
| Redmine REST client | `RedmineApi.cs` (issues, threads, search, batch; `IssueSummary`/`IssueComment`/`IssueThread`) |
| Ticket actions | `OpenTicketCommand.cs`, `CopyTicketLinkCommand.cs`, `RichLinkClipboard.cs` (CF_HTML via Win32) |
| Right-pane details | `TicketDetails.cs` |
| Comments | `CommentsPage.cs` |
| Saved queries | `SavedQuery.cs` (model + `SavedQueryStore`), `SavedQueryForm.cs`, `SavedQueryPage.cs`, `SavedQueryHubPage.cs`, `SavedQueryText.cs` |
| Settings | `SettingsManager.cs`, `CredentialStore.cs` |

Persisted state lives in the MSIX **LocalState** folder (`saved-queries.json`, `ticket-history.json`);
if `LocalState` is unavailable (unpackaged run), state degrades to in-memory only.

## Known issues

- **Comments page needed Esc twice** to return to the list when reached via a *context* command
  (the old Ctrl+C binding); CmdPal appears to push an extra navigation frame for context-command
  pages (primary/Enter navigation does not). The comments page is now the *primary* command of
  ticket items (Enter), so the issue should no longer be reachable — **needs live-palette
  verification**. If a context command ever navigates to a page again, expect this to resurface;
  a possible fix is explicit `CommandResult.GoToPage(NavigationMode.Push)`.

## More docs

- `.github/copilot-instructions.md` — short CmdPal build/deploy notes and skill index.
- `.github/skills/` — task recipes (Adaptive Card forms, settings, dock bands, fallback commands, publishing).
- [Creating a CmdPal extension](https://learn.microsoft.com/windows/powertoys/command-palette/creating-an-extension) ·
  [Extensibility overview](https://learn.microsoft.com/windows/powertoys/command-palette/extensibility-overview)
