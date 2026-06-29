// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

public partial class RedmineExtensionCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settings;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history;
    private readonly SavedSearchStore _store;

    public RedmineExtensionCommandsProvider()
    {
        DisplayName = "Redmine";
        Icon = new IconInfo(""); // glyph:E8FD

        _settings = new SettingsManager();
        Settings = _settings.Settings;
        _api = new RedmineApi(_settings);
        _history = new TicketHistory(_settings.MaxHistoryRetained);
        _store = new SavedSearchStore();

        // カスタム検索の追加/編集/削除で top-level を更新する。
        _store.Changed += (_, _) => RaiseItemsChanged();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        var commands = new List<ICommandItem>
        {
            new CommandItem(new RedmineExtensionPage(_settings, _api, _history)) { Title = DisplayName },
        };

        foreach (var search in _store.All)
        {
            commands.Add(BuildSavedSearchCommand(search));
        }

        commands.Add(new CommandItem(new AddCustomSearchProjectPage(_api, _store))
        {
            Title = "カスタム検索を追加",
            Icon = new IconInfo(""), // glyph:E710
        });

        return commands.ToArray();
    }

    private CommandItem BuildSavedSearchCommand(SavedSearch search)
    {
        var editPage = new CustomSearchFormPage(
            search.ProjectId is int pid ? new RedmineRef(pid, search.ProjectName ?? string.Empty) : null,
            _api,
            _store,
            search);

        var deleteCommand = new AnonymousCommand(() => _store.Remove(search.Id))
        {
            Name = "削除",
            Icon = new IconInfo(""), // glyph:E74D
            Result = CommandResult.GoHome(),
        };

        return new CommandItem(new CustomSearchPage(search, _api, _history))
        {
            Title = search.Name,
            Subtitle = Describe(search),
            Icon = new IconInfo(""), // glyph:E71C
            MoreCommands = [
                new CommandContextItem(editPage) { Title = "編集" },
                new CommandContextItem(deleteCommand),
            ],
        };
    }

    private static string Describe(SavedSearch search)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(search.ProjectName))
        {
            parts.Add(search.ProjectName);
        }

        if (!string.IsNullOrEmpty(search.TrackerName))
        {
            parts.Add(search.TrackerName);
        }

        if (!string.IsNullOrEmpty(search.StatusName))
        {
            parts.Add(search.StatusName);
        }

        if (!string.IsNullOrEmpty(search.AssigneeName))
        {
            parts.Add(search.AssigneeName);
        }

        var label = parts.Count > 0 ? string.Join(" / ", parts) : "条件なし";
        return search.Mode == "count" ? $"件数 — {label}" : label;
    }
}
