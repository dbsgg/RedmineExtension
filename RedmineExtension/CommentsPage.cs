using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

/// <summary>
/// チケットの説明とコメント一覧。各項目は タイトル=書込み者・日付 /
/// サブタイトル=冒頭 / 右ペイン詳細=全文（末尾に書込み者・日時）。
/// 先頭に説明、続けてコメントを新しい順に並べる。
/// 取得結果はセッションキャッシュし、開き直しは即表示＋裏で更新する。
/// </summary>
internal sealed partial class CommentsPage : ListPage
{
    // id → 直近取得したスレッド（セッション内で共有。ページは項目ごとに作り直されるため static）。
    private static readonly ConcurrentDictionary<int, IssueThread> Cache = new();

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly int _id;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history;
    private readonly Func<string?> _titleProvider;

    private IListItem[] _items;
    private DateTime _loadedAtUtc = DateTime.MinValue;
    private bool _loading;
    private bool _recorded;

    public CommentsPage(int id, RedmineApi api, TicketHistory history, Func<string?> titleProvider)
    {
        _id = id;
        _api = api;
        _history = history;
        _titleProvider = titleProvider;

        Id = $"redmine-comments-{id}";
        Title = $"#{id} の説明・コメント";
        Name = "コメントを表示";
        Icon = new IconInfo(""); // glyph:E90A
        PlaceholderText = "説明・コメントを絞り込み";
        ShowDetails = true;

        // キャッシュがあれば即表示（無ければ読み込み中）。いずれも開いた際に裏で更新する。
        _items = Cache.TryGetValue(id, out var cached)
            ? BuildThreadItems(cached)
            : [new ListItem(new NoOpCommand()) { Title = "読み込み中…" }];
    }

    public override IListItem[] GetItems()
    {
        // ページを実際に開いたタイミングで履歴に記録する（Enter=遷移が主操作のため）。
        if (!_recorded)
        {
            _recorded = true;
            _history.Record(_id, _titleProvider());
        }

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
                _items = [new ListItem(new NoOpCommand()) { Title = "設定で Redmine URL と API キーを入力してください。" }];
                RaiseItemsChanged();
                return;
            }

            var previous = Cache.TryGetValue(_id, out var p) ? p : null;
            var fresh = await _api.GetThreadAsync(_id).ConfigureAwait(false);
            Cache[_id] = fresh;

            // 表示中の内容と変わらなければ再描画しない（フォーカス維持・ちらつき防止）。
            if (previous is null || !SameThread(previous, fresh))
            {
                _items = BuildThreadItems(fresh);
                RaiseItemsChanged();
            }
        }
        catch (Exception ex)
        {
            _items = [
                new ListItem(new OpenUrlCommand(_api.IssueUrl(_id)) { Name = "ブラウザで開く" })
                {
                    Title = $"取得に失敗: {ex.Message}",
                    Subtitle = "Enter でチケットをブラウザで開く",
                },
            ];
            RaiseItemsChanged();
        }
        finally
        {
            _loadedAtUtc = DateTime.UtcNow;
            _loading = false;
        }
    }

    private IListItem[] BuildThreadItems(IssueThread thread)
    {
        var items = new List<IListItem>();
        if (thread.Description is not null)
        {
            items.Add(BuildItem(thread.Description, "説明"));
        }

        // コメントは新しい順に。
        foreach (var comment in thread.Comments.Reverse())
        {
            items.Add(BuildItem(comment, "コメント"));
        }

        return items.Count == 0
            ? [new ListItem(new OpenUrlCommand(_api.IssueUrl(_id)) { Name = "ブラウザで開く" }) { Title = "説明・コメントなし", Subtitle = "Enter でチケットをブラウザで開く" }]
            : items.ToArray();
    }

    private ListItem BuildItem(IssueComment entry, string kind)
    {
        var author = string.IsNullOrEmpty(entry.Author) ? "（不明）" : entry.Author;
        var titleByline = Append(author, FormatDate(entry.CreatedOn, withTime: false));
        var detailByline = Append(author, FormatDate(entry.CreatedOn, withTime: true));
        var isDescription = kind == "説明";

        return new ListItem(new OpenUrlCommand(_api.IssueUrl(_id)) { Name = "ブラウザで開く" })
        {
            Title = titleByline,
            Subtitle = isDescription ? $"【説明】 {Snippet(entry.Notes)}" : Snippet(entry.Notes),
            Details = new Details
            {
                Title = $"#{_id} の{kind}",
                Body = $"{entry.Notes}\n\n— {detailByline}",
            },
            MoreCommands = [
                // Ctrl+C=リンクをコピー（チケットのリッチリンク）。
                new CommandContextItem(new CopyTicketLinkCommand(_api, _id, _history))
                {
                    RequestedShortcut = KeyChordHelpers.FromModifiers(
                        ctrl: true, alt: false, shift: false, win: false,
                        vkey: VirtualKey.C, scanCode: 0),
                },
            ],
            Icon = new IconInfo(""), // glyph:E90A
        };
    }

    // 説明とコメント列（notes と日時）が一致すれば同一とみなす。
    private static bool SameThread(IssueThread a, IssueThread b)
    {
        if (a.Description?.Notes != b.Description?.Notes)
        {
            return false;
        }

        if (a.Comments.Count != b.Comments.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Comments.Count; i++)
        {
            if (a.Comments[i].Notes != b.Comments[i].Notes || a.Comments[i].CreatedOn != b.Comments[i].CreatedOn)
            {
                return false;
            }
        }

        return true;
    }

    private static string Append(string author, string date) =>
        string.IsNullOrEmpty(date) ? author : $"{author} · {date}";

    // ISO 日時をローカルに変換。withTime で時刻(HH:mm)まで含める。
    private static string FormatDate(string iso, bool withTime)
    {
        if (string.IsNullOrEmpty(iso))
        {
            return string.Empty;
        }

        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            var local = dto.ToLocalTime();
            return local.ToString(withTime ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return iso.Length >= 10 ? iso[..10] : iso;
    }

    // コメント冒頭。改行を詰めて最大 80 文字に切り詰める。
    private static string Snippet(string text, int max = 80)
    {
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > max ? collapsed[..max] + "…" : collapsed;
    }
}
