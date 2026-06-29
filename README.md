# Redmine for Command Palette

An unofficial [PowerToys Command Palette](https://learn.microsoft.com/windows/powertoys/command-palette/overview)
extension for working with [Redmine](https://www.redmine.org/) without leaving the palette.

> "Redmine" is a trademark of its respective owner. This is an independent, unofficial extension.

## Features

- **Open an issue by number** — type a ticket number (a leading `#` is allowed) and press Enter to open it in your browser.
- **Copy a link** — press Ctrl+Enter to copy a rich `#<id> <title>` hyperlink to the clipboard (a clickable link in Teams/Outlook/Word; plain text elsewhere).
- **Recent history** — recently opened/copied tickets are listed when the search box is empty. The count is configurable.
- **Saved (custom) searches** — create searches filtered by project, tracker, status, and assignee. Each appears as a top-level command in either *list* mode (fuzzy-filter the results) or *count* mode (show the number of matching issues).

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
