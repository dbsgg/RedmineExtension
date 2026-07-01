// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

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

        // 保存クエリの追加/削除/固定変更で top-level を更新する。
        _store.Changed += (_, _) => RaiseItemsChanged();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        var commands = new List<ICommandItem>
        {
            new CommandItem(new RedmineExtensionPage(_settings, _api, _history)) { Title = DisplayName },
        };

        // top-level に固定されたクエリを個別コマンドとして並べる（固定はクエリごと）。
        foreach (var query in _store.All)
        {
            if (query.PinnedToTopLevel)
            {
                commands.Add(BuildQueryTopLevel(query));
            }
        }

        // 保存クエリのハブ（常に表示。中で 追加(Ctrl+A)・件数・一覧・編集/削除・固定切替）。
        commands.Add(new CommandItem(new SavedQueryHubPage(_store, _api, _history, _settings))
        {
            Title = "保存クエリ",
            Subtitle = "保存クエリの一覧・件数・追加",
            Icon = new IconInfo(""), // glyph:E71C
        });

        return commands.ToArray();
    }

    // 固定クエリの top-level コマンド（Enter=一覧 / Ctrl+Enter=Redmine / 固定解除・編集・削除）。
    private CommandItem BuildQueryTopLevel(SavedQuery query)
    {
        return new CommandItem(new SavedQueryPage(query, _api, _history, _settings, _store))
        {
            Title = SavedQueryText.Title(query),
            Subtitle = SavedQueryText.Describe(query),
            Icon = new IconInfo(""), // glyph:E71C
            MoreCommands = [
                new CommandContextItem(new OpenUrlCommand(_api.IssuesWebUrl(query)) { Name = "Redmine で開く" })
                {
                    RequestedShortcut = KeyChordHelpers.FromModifiers(
                        ctrl: true, alt: false, shift: false, win: false,
                        vkey: VirtualKey.Enter, scanCode: 0),
                },
                new CommandContextItem(new SavedQueryFormPage(_store, query)) { Title = "編集" },
                new CommandContextItem(new AnonymousCommand(() => _store.Remove(query.Id))
                {
                    Name = "削除",
                    Icon = new IconInfo(""), // glyph:E74D
                    Result = CommandResult.GoHome(),
                }),
            ],
        };
    }
}
