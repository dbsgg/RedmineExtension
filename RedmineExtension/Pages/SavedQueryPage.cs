using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

/// <summary>
/// 保存クエリの結果一覧ページ。フィルタ結果を並べ、ホスト標準のクエリ絞り込み(ファジー)で検索する。
/// PageSize 件ずつ取得し、末尾までスクロールすると次のページを追記する(LoadMore)。
/// 開く度に先頭ページから取り直し、その total_count を保存クエリの件数として記録する。
/// </summary>
internal sealed partial class SavedQueryPage : ListPage
{
    // 1 回の取得件数（Redmine API の limit）。末尾スクロールでこの単位で追記する。
    private const int PageSize = 100;

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
    private int _issueCount; // 読み込み済みのチケット項目数。次ページ取得の offset に使う。

    public SavedQueryPage(SavedQuery query, RedmineApi api, TicketHistory history, SettingsManager settings, SavedQueryStore store)
    {
        _query = query;
        _api = api;
        _history = history;
        _settings = settings;
        _store = store;

        Id = $"redmine-query-{query.Id}"; // 安定 Id（固定クエリの top-level キャッシュ重複排除用）
        Title = query.Name;
        Name = Strings.Common.Open;
        Icon = new IconInfo(""); // glyph:E71C
        PlaceholderText = Strings.Queries.ResultsPlaceholder;
        ShowDetails = true; // フォーカス時に右ペインの詳細を表示する

        _items = [new ListItem(new NoOpCommand()) { Title = Strings.Common.Loading }];
    }

    public override IListItem[] GetItems()
    {
        // ページを開く度に(古ければ)先頭ページから取り直し、チケットの変更を反映する。
        if (!_loading && DateTime.UtcNow - _loadedAtUtc > RefreshInterval)
        {
            _loading = true;
            _ = LoadAsync(append: false);
        }

        // 「戻る」は追記(ページング)後も常に最後に来るようここで合成する。
        return [.. _items, _backItem];
    }

    // 末尾に固定表示する「戻る」項目（同一インスタンス再利用でフォーカスのちらつきを防ぐ）。
    private readonly IListItem _backItem = Navigation.BackItem();

    // 失敗後などに間隔を待たず再取得する（Enter=再試行から）。
    private void ForceReload()
    {
        _loadedAtUtc = DateTime.MinValue;
        RaiseItemsChanged(); // GetItems が呼ばれ、即 LoadAsync が走る。
    }

    // 末尾までスクロールしたら次のページを追記する（HasMoreItems が true の間ホストが呼ぶ）。
    public override void LoadMore() => LoadNextPage();

    // 次のページを取得して追記する。残りが無い/取得中なら何もしない。
    // ホストの自動呼び出し(LoadMore)と明示コマンド(Ctrl+L)の両方から使う。
    private void LoadNextPage()
    {
        if (_loading || !HasMoreItems)
        {
            return;
        }

        _loading = true;
        IsLoading = true;
        _ = LoadAsync(append: true);
    }

    // Ctrl+L=さらに読み込む（末尾までスクロールしなくても次の 100 件を追加取得できる）。
    private CommandContextItem LoadMoreContext() =>
        new(new AnonymousCommand(LoadNextPage)
        {
            Name = Strings.Queries.LoadMore,
            Result = CommandResult.KeepOpen(),
        })
        {
            RequestedShortcut = Keybindings.LoadMore,
        };

    private async Task LoadAsync(bool append)
    {
        try
        {
            if (!_api.IsConfigured)
            {
                _items = [_settings.SettingsPrompt()];
                HasMoreItems = false;
                return;
            }

            var offset = append ? _issueCount : 0;
            var (issues, total) = await _api.SearchIssuesAsync(_query, PageSize, offset).ConfigureAwait(false);

            if (append)
            {
                _items = [.. _items, .. issues.Select(BuildIssueItem)];
                _issueCount += issues.Count;
            }
            else
            {
                // 一覧を開いたタイミングで件数を記録する（追加取得なし）。
                _store.UpdateCount(_query.Id, total);

                _issueCount = issues.Count;
                _items = issues.Count == 0
                    ? [
                        new ListItem(new OpenUrlCommand(_api.IssuesWebUrl(_query)) { Name = Strings.Common.OpenInBrowser })
                        {
                            Title = Strings.Queries.NoMatches,
                            Subtitle = Strings.Queries.OpenListHint,
                        },
                      ]
                    : issues.Select(BuildIssueItem).ToArray();
            }

            // 残りがあれば末尾スクロールでさらに取得できる。
            HasMoreItems = _issueCount < total;
        }
        catch (Exception ex)
        {
            // Enter=再試行（設定を直した後の確実な復帰手段）。ブラウザはコンテキストへ。
            _items = [
                new ListItem(new AnonymousCommand(ForceReload)
                {
                    Name = Strings.Common.Retry,
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = Strings.Common.FailedToLoad(ex.Message),
                    Subtitle = Strings.Common.RetryHint,
                    MoreCommands = [
                        new CommandContextItem(new OpenUrlCommand(_api.IssuesWebUrl(_query)) { Name = Strings.Common.OpenInBrowser })
                        {
                            RequestedShortcut = Keybindings.OpenInBrowser,
                        },
                        Navigation.BackContext(),
                        Navigation.HomeContext(),
                    ],
                },
            ];
            HasMoreItems = false;
        }
        finally
        {
            _loadedAtUtc = DateTime.UtcNow;
            _loading = false;
            IsLoading = false;
            RaiseItemsChanged();
        }
    }

    // このクエリの詳細ペイン表示項目（クエリのローカル設定 > 拡張設定の既定値）。
    private IReadOnlyCollection<string> DetailFields =>
        _query.DetailFields ?? _settings.DefaultDetailFields;

    private IListItem BuildIssueItem(IssueSummary issue)
    {
        var url = _api.IssueUrl(issue.Id);
        var subject = issue.Subject;

        // Enter=説明・コメントページ / Ctrl+Enter=ブラウザ（固定ペア）。
        var browser = new OpenTicketCommand(url, issue.Id, () => subject, _history);
        var item = new ListItem(new CommentsPage(issue.Id, _api, _history, () => subject, _settings.DefaultCommentsNewestFirst))
        {
            Title = $"#{issue.Id} {subject}",
            Subtitle = TicketDetails.Subtitle(issue),
            Details = TicketDetails.Build(issue, url, DetailFields),
            Icon = new IconInfo(""), // glyph:E774
        };

        item.MoreCommands = [
            // Ctrl+Enter=ブラウザで開く。
            new CommandContextItem(browser)
            {
                RequestedShortcut = Keybindings.OpenInBrowser,
            },

            // Ctrl+C=リンクをコピー（Windows のコピー慣例）。
            new CommandContextItem(new CopyTicketLinkCommand(_api, issue.Id, _history))
            {
                RequestedShortcut = Keybindings.CopyLink,
            },

            // Ctrl+R=このチケットだけ最新に更新（その場で表示を差し替え）。
            new CommandContextItem(new AnonymousCommand(() => _ = RefreshIssueAsync(issue.Id, item))
            {
                Name = Strings.Common.Refresh,
                Result = CommandResult.KeepOpen(),
            })
            {
                RequestedShortcut = Keybindings.Refresh,
            },

            // Ctrl+L=さらに読み込む（どの項目からでも次ページを取得できる）。
            LoadMoreContext(),

            // Ctrl+S=ステータス変更 / Ctrl+M=コメント追加（簡易編集）。
            TicketEdit.StatusContext(_api, issue.Id),
            TicketEdit.AddCommentContext(_api, issue.Id),

            // Alt+←=前のページへ戻る。
            Navigation.BackContext(),
            Navigation.HomeContext(),
        ];

        return item;
    }

    // チケット単体を再取得し、同一項目の表示をその場で更新する（フォーカス維持のため再生成しない）。
    private async Task RefreshIssueAsync(int id, ListItem item)
    {
        try
        {
            var issue = await _api.GetIssueAsync(id).ConfigureAwait(false);
            item.Title = $"#{issue.Id} {issue.Subject}";
            item.Subtitle = TicketDetails.Subtitle(issue);
            item.Details = TicketDetails.Build(issue, _api.IssueUrl(issue.Id), DetailFields);
        }
        catch
        {
            // 取得失敗は無視（表示は前回のまま）。
        }
    }
}
