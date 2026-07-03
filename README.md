# Redmine for Command Palette

*Read this in [日本語](README.ja.md).*

An unofficial [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview)
extension for working with [Redmine](https://www.redmine.org/) without leaving the palette.

> "Redmine" is a trademark of its respective owner. This is an independent, unofficial extension.

## Features

- **Find an issue by number** — type a ticket number (a leading `#` is allowed). Enter shows the issue's description and comments; details appear in the right pane.
- **Description & comments view** — comments are numbered (`n/total`) and listed in one consistent time order: newest first by default (description last), or press Ctrl+O for oldest first (description on top, like the Redmine web UI).
- **Recent history** — recently viewed/opened/copied tickets are listed when the search box is empty. The count is configurable.
- **Saved queries** — paste a Redmine `query_id`, a raw filter query, or a URL to save it. The *Saved queries* hub lists each query with its cached issue count; individual queries can be pinned as top-level commands.
- **Large result sets** — query results load 100 issues at a time; scrolling to the end of the list fetches the next page automatically, or press Ctrl+L on any item to load the next page explicitly.
- **Quick edits** — change an issue's status (Ctrl+S) or add a comment (Ctrl+M) without leaving the palette. Anything more detailed is one Ctrl+Enter away in the browser.
- **Explicit back navigation** — Alt+← (or the trailing "← back" item) goes back one page, even when Esc is configured to close the palette.

### Keyboard scheme (consistent across pages; every key except the Enter/Ctrl+Enter pair is remappable)

| Key (default) | Action |
|-----|--------|
| Enter | Navigate: ticket → description & comments page, saved query → result list |
| Ctrl+Enter | Open in browser (fixed pair with Enter; not remappable) |
| Ctrl+C | Copy a rich `#<id> <title>` hyperlink (clickable in Teams/Outlook/Word; plain text elsewhere) |
| Ctrl+R | Refresh (re-fetch the ticket / update the query count) |
| Ctrl+L | Load the next page of query results |
| Ctrl+O | Toggle comment order (newest first ⇄ oldest first) |
| Ctrl+S | Change the issue's status |
| Ctrl+M | Add a comment to the issue |
| Ctrl+N / Ctrl+E / Ctrl+Delete | Add / edit / delete a saved query |
| Alt+← | Go back one page |
| Alt+Home | Jump all the way back to the palette home |

## Configuration

Open the extension's **Settings** and set:

- **Redmine URL** — e.g. `https://redmine.example.com`
- **API access key** — your personal Redmine REST API key. It is stored in the **Windows Credential Manager** (Generic Credentials, DPAPI-encrypted per user), not in plain text.
- **History count** — number of recent tickets to show (0–50, dropdown).
- **Saved-query count refresh interval** — how stale a cached issue count may get before it is refreshed in the background (5 min–3 h, dropdown).

### Customization (Appearance & shortcuts)

The main page has an **Appearance & shortcuts** form (表示と操作のカスタマイズ) for everything
beyond the connection basics:

- **Detail pane fields (default)** — multi-select of tracker / status / priority / assignee /
  author / progress / dates / description. Each saved query can override this in its edit form.
- **Default comment order** — newest-first (default) or oldest-first when a comments page first opens (Ctrl+O still toggles per session).
- **Pin new saved queries** — default for the "pin to top level" toggle.
- **Keyboard shortcuts** — the shortcut list is collapsed behind a show/hide button; every command except the fixed Enter/Ctrl+Enter pair can be remapped with `Ctrl+Shift+K`-style text.
  `C+` / `A+` / `S+` / `W+` abbreviations are accepted (`C+S+K` = Ctrl+Shift+K; the last token
  is always the key, so `Ctrl+S` still means the S key). Ctrl / Alt / Win is required; leave a
  field empty to restore the default. Invalid or duplicate bindings are rejected on save.
  Changes apply when pages are reopened.

> Use an `https://` URL where possible: the API key is sent in the `X-Redmine-API-Key` header, which is in cleartext over plain `http://`.

## Language

The UI follows your Windows display language: Japanese when it is Japanese, English otherwise.

## Install

> **Status:** Microsoft Store publication is planned. Until then the extension is
> **developer-sideload only** — Command Palette discovers extensions solely through their
> MSIX registration, so the EXE installers produced by `build-exe.ps1` install fine but are
> *not* picked up by CmdPal (see `RELEASING.md`).

Until the Store listing is live:

1. Enable Windows **Developer Mode**.
2. Clone the repo, open in Visual Studio, and use **Build → Deploy** (installs the MSIX).
3. Run **Reload** inside Command Palette.

## Build & run (development)

Requires Visual Studio with the C#/WinUI workloads and the Windows App SDK.

```sh
dotnet build RedmineExtension.sln -c Debug -p:Platform=x64
```

In Visual Studio: select the **(Package)** launch profile, use **Build → Deploy** to register the MSIX package, then run **Reload** inside Command Palette.

## License

[MIT](LICENSE).
