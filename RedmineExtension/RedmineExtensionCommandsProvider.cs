// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
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
    private readonly SavedQueryHubPage _hub;
    private readonly RedmineExtensionPage _mainPage;

    // queryId → 固定表示中の top-level 項目。件数更新でタイトルだけ差し替えるため再利用する。
    private readonly Dictionary<string, CommandItem> _pinnedItems = new();

    public RedmineExtensionCommandsProvider()
    {
        DisplayName = "Redmine";
        Icon = new IconInfo(""); // glyph:E8FD

        // カスタマイズ（キーバインド上書き等）は他の全構築より先に読み込む。
        var uiConfig = new UiConfigStore();
        Keybindings.Configure(uiConfig);

        _settings = new SettingsManager(uiConfig);
        Settings = _settings.Settings;
        _api = new RedmineApi(_settings);
        _history = new TicketHistory(_settings.MaxHistoryRetained);
        _store = new SavedQueryStore();

        // ページはセッションで共有する（キャッシュ・状態を保つ）。ハブは番号検索ページからも遷移できる。
        _hub = new SavedQueryHubPage(_store, _api, _history, _settings, uiConfig);
        _mainPage = new RedmineExtensionPage(_settings, _api, _history, _hub, new CustomizePage(uiConfig, _settings), uiConfig);

        // カスタマイズ保存（Enter 入れ替え等）で固定項目を作り直す。
        uiConfig.Changed += (_, _) =>
        {
            _pinnedItems.Clear();
            RaiseItemsChanged();
        };

        // 保存クエリの追加/削除/固定変更で top-level を更新する（名前等も変わるため作り直す）。
        _store.Changed += (_, _) =>
        {
            _pinnedItems.Clear();
            RaiseItemsChanged();
        };

        // 件数の記録更新は固定項目のタイトルへその場反映する（一覧ページを開いた時なども含む）。
        _store.CountChanged += (_, _) => SyncPinnedTitles();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        var commands = new List<ICommandItem>
        {
            // タイトルは動作名にする（DisplayName="Redmine" と同名にすると一覧で二重に見えるため）。
            new CommandItem(_mainPage) { Title = Strings.Tickets.MainCommandTitle },
        };

        // top-level に固定されたクエリを個別コマンドとして並べる（固定はクエリごと）。
        // 項目は再利用し、件数変化は CountChanged 経由でタイトルのみ更新する。
        foreach (var query in _store.All)
        {
            if (query.PinnedToTopLevel)
            {
                if (!_pinnedItems.TryGetValue(query.Id, out var item))
                {
                    item = BuildQueryTopLevel(query);
                    _pinnedItems[query.Id] = item;
                }

                commands.Add(item);
            }
        }

        // 保存クエリのハブ（常に表示。中で 追加(Ctrl+N)・件数・一覧・編集/削除・固定切替）。
        commands.Add(new CommandItem(_hub)
        {
            Title = Strings.Queries.HubTitle,
            Subtitle = Strings.Queries.HubSubtitle,
            Icon = new IconInfo(""), // glyph:E71C
        });

        return commands.ToArray();
    }

    // 固定クエリの top-level コマンド（Enter=一覧 / Ctrl+Enter=ブラウザ / 編集・固定解除・削除）。
    private CommandItem BuildQueryTopLevel(SavedQuery query)
    {
        var web = new OpenUrlCommand(_api.IssuesWebUrl(query)) { Name = Strings.Common.OpenInBrowser };

        return new CommandItem(new SavedQueryPage(query, _api, _history, _settings, _store))
        {
            Title = SavedQueryText.Title(query),
            Subtitle = SavedQueryText.Describe(query),
            Icon = new IconInfo(""), // glyph:E71C
            MoreCommands = [
                // Ctrl+Enter=ブラウザで開く（Redmine のフィルタ結果）
                new CommandContextItem(web)
                {
                    RequestedShortcut = Keybindings.OpenInBrowser,
                },

                // Ctrl+R=件数を最新に更新（トップレベルからも件数を更新できるように）
                new CommandContextItem(new AnonymousCommand(() => _ = RefreshCountAsync(query))
                {
                    Name = Strings.Queries.RefreshCount,
                    Result = CommandResult.KeepOpen(),
                })
                {
                    RequestedShortcut = Keybindings.Refresh,
                },

                // Ctrl+E=編集
                new CommandContextItem(new SavedQueryFormPage(_store, _settings, query))
                {
                    Title = Strings.Common.Edit,
                    RequestedShortcut = Keybindings.EditQuery,
                },

                // 固定を解除（top-level から外す。クエリ自体は残る）。
                new CommandContextItem(new AnonymousCommand(() =>
                {
                    query.PinnedToTopLevel = false;
                    _store.AddOrUpdate(query);
                })
                {
                    Name = Strings.Queries.UnpinFromTopLevel,
                    Result = CommandResult.KeepOpen(),
                }),
                new CommandContextItem(new AnonymousCommand(() => _store.Remove(query.Id))
                {
                    Name = Strings.Common.Delete,
                    Icon = new IconInfo(""), // glyph:E74D
                    Result = CommandResult.GoHome(),
                })
                {
                    // Ctrl+Delete=削除
                    RequestedShortcut = Keybindings.DeleteQuery,
                },
            ],
        };
    }

    // 件数の記録更新を、固定表示中の top-level 項目のタイトルへその場反映する。
    private void SyncPinnedTitles()
    {
        foreach (var query in _store.All)
        {
            if (_pinnedItems.TryGetValue(query.Id, out var item))
            {
                item.Title = SavedQueryText.Title(query);
            }
        }
    }

    // 件数を再取得して記録する（UpdateCount → CountChanged 経由で表示が追従する）。
    private async Task RefreshCountAsync(SavedQuery query)
    {
        try
        {
            var (_, total) = await _api.SearchIssuesAsync(query, 1).ConfigureAwait(false);
            _store.UpdateCount(query.Id, total);
        }
        catch
        {
            // 取得失敗は無視（前回値のまま）。
        }
    }
}
