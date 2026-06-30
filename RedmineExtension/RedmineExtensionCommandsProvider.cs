// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

public partial class RedmineExtensionCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settings;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history;
    private readonly SavedQueryStore _store;

    public RedmineExtensionCommandsProvider()
    {
        DisplayName = "Redmine";
        Icon = new IconInfo(""); // glyph:E8FD

        _settings = new SettingsManager();
        Settings = _settings.Settings;
        _api = new RedmineApi(_settings);
        _history = new TicketHistory(_settings.MaxHistoryRetained);
        _store = new SavedQueryStore();

        // 保存クエリの追加/編集/削除で top-level を更新する。
        _store.Changed += (_, _) => RaiseItemsChanged();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        var commands = new List<ICommandItem>
        {
            new CommandItem(new RedmineExtensionPage(_settings, _api, _history)) { Title = DisplayName },
        };

        foreach (var query in _store.All)
        {
            commands.Add(BuildSavedQueryCommand(query));
        }

        commands.Add(new CommandItem(new SavedQueryProjectPage(_api, _store))
        {
            Title = "保存クエリを追加",
            Icon = new IconInfo(""), // glyph:E710
        });

        return commands.ToArray();
    }

    private CommandItem BuildSavedQueryCommand(SavedQuery query)
    {
        var editPage = new SavedQueryFormPage(
            query.ProjectId is int pid ? new RedmineRef(pid, query.ProjectName ?? string.Empty) : null,
            _api,
            _store,
            query);

        var deleteCommand = new AnonymousCommand(() => _store.Remove(query.Id))
        {
            Name = "削除",
            Icon = new IconInfo(""), // glyph:E74D
            Result = CommandResult.GoHome(),
        };

        return new CommandItem(new SavedQueryPage(query, _api, _history))
        {
            Title = query.Name,
            Subtitle = Describe(query),
            Icon = new IconInfo(""), // glyph:E71C
            MoreCommands = [
                new CommandContextItem(editPage) { Title = "編集" },
                new CommandContextItem(deleteCommand),
            ],
        };
    }

    private static string Describe(SavedQuery query)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(query.ProjectName))
        {
            parts.Add(query.ProjectName);
        }

        AddCondition(parts, "トラッカー", query.Tracker);
        AddCondition(parts, "ステータス", query.Status);
        AddCondition(parts, "担当者", query.Assignee);

        var label = parts.Count > 0 ? string.Join(" / ", parts) : "条件なし";
        return query.Mode == "count" ? $"件数 — {label}" : label;
    }

    private static void AddCondition(List<string> parts, string label, FilterCondition condition)
    {
        if (condition is null || !condition.HasFilter)
        {
            return;
        }

        var names = string.Join(",", condition.Values.Select(v => v.Name));
        var text = condition.Op switch
        {
            "o" => $"{label}:未完了",
            "c" => $"{label}:完了",
            "*" => $"{label}:すべて",
            "=" => $"{label}:{names}",
            "!" => $"{label}≠{names}",
            _ => null,
        };

        if (!string.IsNullOrEmpty(text))
        {
            parts.Add(text);
        }
    }
}
