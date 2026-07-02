using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

/// <summary>
/// 保存クエリのハブ。全クエリを「{名前}: {N}件」で一覧表示する。
/// Enter で一覧ページへ、Ctrl+Enter でブラウザで開く。Ctrl+R で件数更新、
/// Ctrl+N で追加、Ctrl+E で編集、Ctrl+Delete で削除。固定切替はコンテキストから。
/// 件数は記録値を即表示し、古い(TTL超過)ものだけ開いた際に裏で再取得する。
/// </summary>
internal sealed partial class SavedQueryHubPage : ListPage
{
    // 件数の鮮度。これを超えたら開いた際に再取得する。
    private static readonly TimeSpan CountTtl = TimeSpan.FromMinutes(30);

    // GetItems 連続呼び出しでの多重更新を防ぐ間隔。
    private static readonly TimeSpan RefreshThrottle = TimeSpan.FromSeconds(2);

    // 件数の連続取得の間隔（サーバー負荷を抑えるため各取得の間を空ける）。
    private static readonly TimeSpan RefreshSpacing = TimeSpan.FromMilliseconds(500);

    private readonly SavedQueryStore _store;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history;
    private readonly SettingsManager _settings;

    private readonly Dictionary<string, ListItem> _itemsById = new();
    private IListItem[] _items;
    private bool _built;
    private bool _refreshing;
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    public SavedQueryHubPage(SavedQueryStore store, RedmineApi api, TicketHistory history, SettingsManager settings)
    {
        _store = store;
        _api = api;
        _history = history;
        _settings = settings;

        Title = "保存クエリ";
        Name = "開く";
        Icon = new IconInfo(""); // glyph:E71C
        PlaceholderText = "保存クエリを選択（Ctrl+N で追加）";

        _items = [new ListItem(new NoOpCommand()) { Title = "読み込み中…" }];

        // 追加/編集/削除で作り直す。
        _store.Changed += (_, _) =>
        {
            _built = false;
            RaiseItemsChanged();
        };
    }

    public override IListItem[] GetItems()
    {
        if (!_built)
        {
            _built = true;
            BuildItems();
        }

        SyncTitles();
        MaybeRefreshCounts();
        return _items;
    }

    private void BuildItems()
    {
        _itemsById.Clear();

        if (!_api.IsConfigured)
        {
            _items = [_settings.SettingsPrompt()];
            return;
        }

        var list = new List<IListItem>();
        foreach (var query in _store.All)
        {
            var item = BuildQueryItem(query);
            _itemsById[query.Id] = item;
            list.Add(item);
        }

        list.Add(BuildAddItem());
        _items = list.ToArray();
    }

    private ListItem BuildAddItem()
    {
        var item = new ListItem(new SavedQueryFormPage(_store))
        {
            Title = "＋ 保存クエリを追加",
            Icon = new IconInfo(""), // glyph:E710
        };
        item.MoreCommands = [AddContext()];
        return item;
    }

    // Ctrl+N で保存クエリを追加（新規作成の慣例。Ctrl+A は検索ボックスの全選択と衝突するため使わない）。
    private CommandContextItem AddContext() =>
        new(new SavedQueryFormPage(_store))
        {
            Title = "保存クエリを追加",
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true, alt: false, shift: false, win: false,
                vkey: VirtualKey.N, scanCode: 0),
        };

    private ListItem BuildQueryItem(SavedQuery query)
    {
        // Enter=一覧ページへ遷移
        var item = new ListItem(new SavedQueryPage(query, _api, _history, _settings, _store))
        {
            Title = SavedQueryText.Title(query),
            Subtitle = SavedQueryText.Describe(query),
            Icon = new IconInfo(""), // glyph:E8EC
        };

        item.MoreCommands = [
            // Ctrl+Enter=ブラウザで開く（Redmine のフィルタ結果）
            new CommandContextItem(new OpenUrlCommand(_api.IssuesWebUrl(query)) { Name = "ブラウザで開く" })
            {
                RequestedShortcut = KeyChordHelpers.FromModifiers(
                    ctrl: true, alt: false, shift: false, win: false,
                    vkey: VirtualKey.Enter, scanCode: 0),
            },

            // Ctrl+R=件数を最新に更新
            new CommandContextItem(new AnonymousCommand(() => _ = FetchCountAsync(query, item))
            {
                Name = "件数を最新に更新",
                Result = CommandResult.KeepOpen(),
            })
            {
                RequestedShortcut = KeyChordHelpers.FromModifiers(
                    ctrl: true, alt: false, shift: false, win: false,
                    vkey: VirtualKey.R, scanCode: 0),
            },

            AddContext(),

            // Ctrl+E=編集
            new CommandContextItem(new SavedQueryFormPage(_store, query))
            {
                Title = "編集",
                RequestedShortcut = KeyChordHelpers.FromModifiers(
                    ctrl: true, alt: false, shift: false, win: false,
                    vkey: VirtualKey.E, scanCode: 0),
            },

            // トップレベルへの固定/解除（store.Changed で一覧と top-level が作り直される）。
            new CommandContextItem(new AnonymousCommand(() => TogglePin(query))
            {
                Name = query.PinnedToTopLevel ? "トップレベルの固定を解除" : "トップレベルに固定",
                Result = CommandResult.KeepOpen(),
            }),

            // Ctrl+Delete=削除
            new CommandContextItem(new AnonymousCommand(() => _store.Remove(query.Id))
            {
                Name = "削除",
                Result = CommandResult.KeepOpen(),
            })
            {
                RequestedShortcut = KeyChordHelpers.FromModifiers(
                    ctrl: true, alt: false, shift: false, win: false,
                    vkey: VirtualKey.Delete, scanCode: 0),
            },
        ];

        return item;
    }

    // 固定状態を反転して保存する（AddOrUpdate が Changed を発火し、表示側が追従する）。
    private void TogglePin(SavedQuery query)
    {
        query.PinnedToTopLevel = !query.PinnedToTopLevel;
        _store.AddOrUpdate(query);
    }

    // 記録済み件数を項目タイトルへ反映（一覧ページを開いた等で更新された値を追う。API なし）。
    private void SyncTitles()
    {
        foreach (var query in _store.All)
        {
            if (_itemsById.TryGetValue(query.Id, out var item))
            {
                var title = SavedQueryText.Title(query);
                if (item.Title != title)
                {
                    item.Title = title;
                }
            }
        }
    }

    // 開くたびに、古い件数(TTL超過)だけ裏で再取得する。
    private void MaybeRefreshCounts()
    {
        if (_refreshing || !_api.IsConfigured || DateTime.UtcNow - _lastRefreshUtc < RefreshThrottle)
        {
            return;
        }

        var now = DateTime.UtcNow;
        _lastRefreshUtc = now;

        // 最も更新の古いもの（未取得=null を最優先）から取得する。
        var stale = _store.All
            .Where(q => q.CountUpdatedUtc is null || now - q.CountUpdatedUtc.Value > CountTtl)
            .OrderBy(q => q.CountUpdatedUtc ?? DateTime.MinValue)
            .ToList();
        if (stale.Count == 0)
        {
            return;
        }

        _refreshing = true;
        _ = Task.Run(async () =>
        {
            foreach (var query in stale)
            {
                if (_itemsById.TryGetValue(query.Id, out var item))
                {
                    await FetchCountAsync(query, item).ConfigureAwait(false);
                    await Task.Delay(RefreshSpacing).ConfigureAwait(false);
                }
            }

            _refreshing = false;
        });
    }

    // 件数を再取得して記録＋項目タイトルをその場更新（フォーカス維持のため RaiseItemsChanged しない）。
    private async Task FetchCountAsync(SavedQuery query, ListItem item)
    {
        try
        {
            var (_, total) = await _api.SearchIssuesAsync(query, 1).ConfigureAwait(false);
            _store.UpdateCount(query.Id, total);
            query.Count = total;
            item.Title = SavedQueryText.Title(query);
        }
        catch
        {
            // 取得失敗は無視（前回値のまま）。
        }
    }

}
