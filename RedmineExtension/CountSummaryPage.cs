using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// 件数モードの保存クエリを 1 ページにまとめて「{名前}: {N} 件」で一覧表示する。
/// 各項目は Redmine の該当一覧を開き、編集/削除も行える。
/// </summary>
internal sealed partial class CountSummaryPage : ListPage
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly SavedQueryStore _store;
    private readonly RedmineApi _api;
    private readonly SettingsManager _settings;

    private IListItem[] _items;
    private DateTime _loadedAtUtc = DateTime.MinValue;
    private bool _loading;

    public CountSummaryPage(SavedQueryStore store, RedmineApi api, SettingsManager settings)
    {
        _store = store;
        _api = api;
        _settings = settings;

        Title = "保存クエリ（件数）";
        Name = "Open";
        Icon = new IconInfo(""); // glyph:E8EC
        PlaceholderText = "件数モードの保存クエリ";

        _items = [new ListItem(new NoOpCommand()) { Title = "読み込み中…" }];

        // 追加/編集/削除で再取得する。
        _store.Changed += (_, _) =>
        {
            _loadedAtUtc = DateTime.MinValue;
            RaiseItemsChanged();
        };
    }

    public override IListItem[] GetItems()
    {
        if (!_loading && DateTime.UtcNow - _loadedAtUtc > RefreshInterval)
        {
            _loading = true;
            _ = LoadAsync();
        }

        return _items;
    }

    private async Task LoadAsync()
    {
        try
        {
            if (!_api.IsConfigured)
            {
                _items = [_settings.SettingsPrompt()];
                return;
            }

            var countQueries = _store.All.Where(q => q.Mode == "count").ToArray();
            if (countQueries.Length == 0)
            {
                _items = [new ListItem(new NoOpCommand()) { Title = "件数モードの保存クエリがありません" }];
                return;
            }

            var items = new List<IListItem>();
            foreach (var query in countQueries)
            {
                items.Add(await BuildCountItem(query).ConfigureAwait(false));
            }

            _items = items.ToArray();
        }
        catch (Exception ex)
        {
            _items = [new ListItem(new NoOpCommand()) { Title = $"取得に失敗: {ex.Message}" }];
        }
        finally
        {
            _loadedAtUtc = DateTime.UtcNow;
            _loading = false;
            RaiseItemsChanged();
        }
    }

    private async Task<IListItem> BuildCountItem(SavedQuery query)
    {
        string title;
        try
        {
            var (_, total) = await _api.SearchIssuesAsync(query, 1).ConfigureAwait(false);
            title = $"{query.Name}: {total} 件";
        }
        catch
        {
            title = $"{query.Name}: 取得失敗";
        }

        return new ListItem(new OpenUrlCommand(_api.IssuesWebUrl(query)))
        {
            Title = title,
            Subtitle = "Redmine で一覧を開く",
            Icon = new IconInfo(""), // glyph:E8EC
            MoreCommands = [
                new CommandContextItem(new SavedQueryFormPage(_store, query)) { Title = "編集" },
                new CommandContextItem(new AnonymousCommand(() => _store.Remove(query.Id))
                {
                    Name = "削除",
                    Result = CommandResult.KeepOpen(),
                }),
            ],
        };
    }
}
