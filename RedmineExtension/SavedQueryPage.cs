using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

/// <summary>
/// 保存クエリの結果一覧ページ。フィルタ結果を並べ、ホスト標準のクエリ絞り込み(ファジー)で検索する。
/// 開く度に取得し、その total_count を保存クエリの件数として記録する。
/// </summary>
internal sealed partial class SavedQueryPage : ListPage
{
    // 連続呼び出しでの多重取得は防ぎつつ、開く度に最新を反映するための間隔。
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly SavedQuery _query;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history;
    private readonly SettingsManager _settings;
    private readonly SavedQueryStore _store;

    private IListItem[] _items;
    private DateTime _loadedAtUtc = DateTime.MinValue;
    private bool _loading;

    public SavedQueryPage(SavedQuery query, RedmineApi api, TicketHistory history, SettingsManager settings, SavedQueryStore store)
    {
        _query = query;
        _api = api;
        _history = history;
        _settings = settings;
        _store = store;

        Title = query.Name;
        Name = "Open";
        Icon = new IconInfo(""); // glyph:E71C
        PlaceholderText = "クエリでファジー絞り込み";
        ShowDetails = true; // フォーカス時に右ペインの詳細を表示する

        _items = [new ListItem(new NoOpCommand()) { Title = "読み込み中…" }];
    }

    public override IListItem[] GetItems()
    {
        // ページを開く度に(古ければ)再取得し、チケットの変更を反映する。
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

            var (issues, total) = await _api.SearchIssuesAsync(_query, 100).ConfigureAwait(false);

            // 一覧を開いたタイミングで件数を記録する（追加取得なし）。
            _store.UpdateCount(_query.Id, total);

            _items = issues.Count == 0
                ? [
                    new ListItem(new OpenUrlCommand(_api.IssuesWebUrl(_query)))
                    {
                        Title = "該当チケットなし",
                        Subtitle = "Redmine で開く",
                    },
                  ]
                : issues.Select(BuildIssueItem).ToArray();
        }
        catch (Exception ex)
        {
            _items = [
                new ListItem(new OpenUrlCommand(_api.IssuesWebUrl(_query)))
                {
                    Title = $"取得に失敗: {ex.Message}",
                    Subtitle = "Redmine で開く",
                },
            ];
        }
        finally
        {
            _loadedAtUtc = DateTime.UtcNow;
            _loading = false;
            RaiseItemsChanged();
        }
    }

    private IListItem BuildIssueItem(IssueSummary issue)
    {
        var url = _api.IssueUrl(issue.Id);
        var subject = issue.Subject;

        return new ListItem(new OpenTicketCommand(url, issue.Id, () => subject, _history))
        {
            Title = $"#{issue.Id} {subject}",
            Subtitle = TicketDetails.Subtitle(issue),
            Details = TicketDetails.Build(issue, url),
            Icon = new IconInfo(""), // glyph:E774
            MoreCommands = [
                new CommandContextItem(new CopyTicketLinkCommand(_api, issue.Id, _history))
                {
                    RequestedShortcut = KeyChordHelpers.FromModifiers(
                        ctrl: true, alt: false, shift: false, win: false,
                        vkey: VirtualKey.Enter, scanCode: 0),
                },
                new CommandContextItem(new CommentsPage(issue.Id, _api))
                {
                    Title = "コメント",
                    RequestedShortcut = KeyChordHelpers.FromModifiers(
                        ctrl: true, alt: false, shift: false, win: false,
                        vkey: VirtualKey.C, scanCode: 0),
                },
            ],
        };
    }
}
