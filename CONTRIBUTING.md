# Contributing

Thanks for your interest! This project welcomes issues and pull requests.

## Ground rules

- **Read [CLAUDE.md](CLAUDE.md) first.** It is the canonical guide for both humans and
  AI coding agents: hard rules (warning-free builds, AOT/trim safety, credential handling),
  conventions (string catalog, keybinding catalog, icon-glyph workaround), and the code map.
- **Keep the build warning-free.** Debug builds treat analyzer warnings as errors; CI enforces this.
- **UI strings** go in `RedmineExtension/Strings.cs` (Japanese + English via `L10n`). Never
  hardcode user-visible text in pages or commands.
- **Keyboard shortcuts** go in `RedmineExtension/Keybindings.cs`. Never call
  `KeyChordHelpers.FromModifiers` from page code.
- **Never** store or log the Redmine API key outside the Windows Credential Manager.

## Development

```sh
dotnet build RedmineExtension.sln -c Debug -p:Platform=x64
```

Running the extension requires Visual Studio → Build → **Deploy** (installs the MSIX),
then **Reload** inside Command Palette. There is no automated UI test; describe your manual
verification steps in the PR.

## Pull requests

- Work on a feature branch and keep history linear (squash before merging).
- Match the existing style (`.editorconfig`); comments and UI text are Japanese-first.
- If you used an AI agent, that's fine — but you are responsible for the diff. Confirm the
  hard rules in CLAUDE.md were followed (especially credentials and analyzer warnings).
