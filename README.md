# Redmine for Command Palette

An unofficial [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview)
extension for working with [Redmine](https://www.redmine.org/) without leaving the palette.

> "Redmine" is a trademark of its respective owner. This is an independent, unofficial extension.

## Features

- **Find an issue by number** — type a ticket number (a leading `#` is allowed). Enter shows the issue's description and comments; details appear in the right pane.
- **Recent history** — recently viewed/opened/copied tickets are listed when the search box is empty. The count is configurable.
- **Saved queries** — paste a Redmine `query_id`, a raw filter query, or a URL to save it. The *Saved queries* hub lists each query with its cached issue count; individual queries can be pinned as top-level commands.
- **Large result sets** — query results load 100 issues at a time; scrolling to the end of the list fetches the next page automatically, or press Ctrl+L on any item to load the next page explicitly.

### Keyboard scheme (consistent across pages)

| Key | Action |
|-----|--------|
| Enter | Navigate: ticket → description & comments page, saved query → result list |
| Ctrl+Enter | Open in browser (ticket page / Redmine filter results) |
| Ctrl+C | Copy a rich `#<id> <title>` hyperlink (clickable in Teams/Outlook/Word; plain text elsewhere) |
| Ctrl+R | Refresh (re-fetch the ticket / update the query count) |
| Ctrl+L | Load the next page of query results |
| Ctrl+N | Add a saved query |
| Ctrl+E | Edit the saved query |
| Ctrl+Delete | Delete the saved query |

## Configuration

Open the extension's **Settings** and set:

- **Redmine URL** — e.g. `https://redmine.example.com`
- **API access key** — your personal Redmine REST API key. It is stored in the **Windows Credential Manager** (Generic Credentials, DPAPI-encrypted per user), not in plain text.
- **History count** — number of recent tickets to show (0–50).

> Use an `https://` URL where possible: the API key is sent in the `X-Redmine-API-Key` header, which is in cleartext over plain `http://`.

## Build & run (development)

Requires Visual Studio with the C#/WinUI workloads and the Windows App SDK.

```sh
dotnet build RedmineExtension.sln -c Debug -p:Platform=x64
```

In Visual Studio: select the **(Package)** launch profile, use **Build → Deploy** to register the MSIX package, then run **Reload** inside Command Palette.

## License

[MIT](LICENSE).
