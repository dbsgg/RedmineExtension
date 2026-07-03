using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

/// <summary>
/// チケットの説明とコメント一覧（Enter でチケット項目から遷移する）。
/// 時系列を一方向に揃えて並べる: 既定は新しい順（最新コメント → … → 最初のコメント → 説明）、
/// Ctrl+O で古い順（説明 → 最初のコメント → … = Redmine の Web 表示と同じ）に切り替え。
/// コメントには古い方から「n/総数」の通番を振り、位置が一目でわかるようにする。
/// 取得結果はセッションキャッシュし、開き直しは即表示＋裏で更新する。
/// </summary>
internal sealed partial class CommentsPage : ListPage
{
    // id → 直近取得したスレッド（セッション内で共有。ページは項目ごとに作り直されるため static）。
    private static readonly ConcurrentDictionary<int, IssueThread> Cache = new();

    /// <summary>コメント追加・ステータス変更の後に呼び、次回表示で再取得させる。</summary>
    public static void Invalidate(int id) => Cache.TryRemove(id, out _);

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private static readonly IconInfo CommentIcon = new IconInfo(""); // glyph:E90A
    private static readonly IconInfo DescriptionIcon = new IconInfo(""); // glyph:E8A5

    // 並び順（true=新しい順）。セッション内の全コメントページで共有・記憶する。
    private static bool _newestFirst = true;

    private readonly int _id;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history;
    private readonly Func<string?> _titleProvider;

    private IListItem[] _items;
    private IssueThread? _thread;
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
        Title = Strings.Comments.PageTitle(id);
        Name = Strings.Comments.CommandName;
        Icon = new IconInfo(""); // glyph:E90A
        PlaceholderText = Strings.Comments.FilterPlaceholder;
        ShowDetails = true;

        // キャッシュがあれば即表示（無ければ読み込み中）。いずれも開いた際に裏で更新する。
        _items = Cache.TryGetValue(id, out var cached)
            ? BuildThreadItems(cached)
            : [new ListItem(new NoOpCommand()) { Title = Strings.Common.Loading }];
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
                _items = [
                    new ListItem(new NoOpCommand()) { Title = Strings.Setup.ConfigureFirst },
                    Navigation.BackItem(),
                ];
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
                new ListItem(new OpenUrlCommand(_api.IssueUrl(_id)) { Name = Strings.Common.OpenInBrowser })
                {
                    Title = Strings.Common.FailedToLoad(ex.Message),
                    Subtitle = Strings.Tickets.OpenTicketHint,
                },
                Navigation.BackItem(),
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
        _thread = thread;

        // コメントには古い方から 1..N の通番を振る（Redmine の注記番号と同じ向き）。
        var total = thread.Comments.Count;
        var comments = new List<IListItem>(total);
        for (var i = 0; i < total; i++)
        {
            comments.Add(BuildCommentItem(thread.Comments[i], i + 1, total));
        }

        if (_newestFirst)
        {
            // 新しい順: 最新コメント → … → 最初のコメント → 説明（時系列降順で一貫させる）。
            comments.Reverse();
        }

        var items = new List<IListItem>();

        // 説明項目は本文が空でも常に置く（Details にチケットの基本情報を全量表示するため）。
        var descEntry = thread.Description
            ?? new IssueComment(thread.Issue.Author ?? string.Empty, string.Empty, thread.Issue.CreatedOn ?? string.Empty);
        var description = BuildDescriptionItem(descEntry, thread.Issue);

        // 古い順のときは説明（最古）が先頭、新しい順のときは末尾に来る。
        if (!_newestFirst)
        {
            items.Add(description);
        }

        items.AddRange(comments);

        if (_newestFirst)
        {
            items.Add(description);
        }

        // 末尾に簡易操作と戻る導線（Esc が「閉じる」設定でも戻れるように）。
        items.Add(new ListItem(new AddCommentPage(_api, _id)) { Title = Strings.Comments.AddCommentItem });
        items.Add(Navigation.BackItem());

        return items.ToArray();
    }

    // コメント項目。タイトルに通番「n/総数」を入れて位置が一目でわかるようにする。
    private ListItem BuildCommentItem(IssueComment entry, int number, int total)
    {
        var label = Strings.Comments.CommentLabel(number, total);
        var detailByline = Append(
            string.IsNullOrEmpty(entry.Author) ? Strings.Comments.UnknownAuthor : entry.Author,
            FormatDate(entry.CreatedOn, withTime: true));

        return BuildEntryItem(
            entry,
            label,
            CommentIcon,
            detailsTitle: Strings.Comments.DetailTitle(_id, label),
            detailsBody: $"{entry.Notes}\n\n— {detailByline}");
    }

    // 説明（チケット本文）の項目。コメントとはアイコンを変えて区別する。
    // Details のタイトルは「#番号 チケットタイトル」（項目名で説明だと分かるため）。
    // 本文の下に区切りを入れ、同じレスポンスで取得済みの基本情報を全項目1行ずつ並べる。
    private ListItem BuildDescriptionItem(IssueComment entry, IssueSummary issue)
    {
        if (string.IsNullOrWhiteSpace(entry.Notes))
        {
            entry = entry with { Notes = Strings.Comments.NoDescription };
        }

        return BuildEntryItem(
            entry,
            Strings.Comments.DescriptionLabel,
            DescriptionIcon,
            detailsTitle: $"#{_id} {issue.Subject}".TrimEnd(),
            detailsBody: $"{entry.Notes}\n\n---\n\n{TicketDetails.FieldLines(issue)}");
    }

    private ListItem BuildEntryItem(IssueComment entry, string label, IconInfo icon, string detailsTitle, string detailsBody)
    {
        var author = string.IsNullOrEmpty(entry.Author) ? Strings.Comments.UnknownAuthor : entry.Author;
        var titleByline = Append(author, FormatDate(entry.CreatedOn, withTime: false));

        // Enter は割り当てない（遷移先が無いため）。ブラウザは Ctrl+Enter で統一。
        return new ListItem(new NoOpCommand())
        {
            Title = $"{label} · {titleByline}",
            Subtitle = Snippet(entry.Notes),
            Details = new Details
            {
                Title = detailsTitle,
                Body = detailsBody,
            },
            MoreCommands = [
                // Ctrl+Enter=ブラウザで開く（他ページのチケット項目とキーを統一）。
                new CommandContextItem(new OpenUrlCommand(_api.IssueUrl(_id)) { Name = Strings.Common.OpenInBrowser })
                {
                    RequestedShortcut = Keybindings.OpenInBrowser,
                },

                // Ctrl+C=リンクをコピー（チケットのリッチリンク）。
                new CommandContextItem(new CopyTicketLinkCommand(_api, _id, _history))
                {
                    RequestedShortcut = Keybindings.CopyLink,
                },

                // Ctrl+O=並び順(Order)の切り替え。
                OrderToggleContext(),

                // Ctrl+M=コメントを追加 / Ctrl+S=ステータスを変更（簡易編集）。
                TicketEdit.AddCommentContext(_api, _id),
                TicketEdit.StatusContext(_api, _id),

                // Alt+←=前のページへ戻る。
                Navigation.BackContext(),
            Navigation.HomeContext(),
            ],
            Icon = icon,
        };
    }

    // 並び順の切り替えコマンド。現在の並びに応じて「次に何が起きるか」を名前に出す。
    private CommandContextItem OrderToggleContext() =>
        new(new AnonymousCommand(ToggleOrder)
        {
            Name = _newestFirst ? Strings.Comments.ShowOldestFirst : Strings.Comments.ShowNewestFirst,
            Result = CommandResult.KeepOpen(),
        })
        {
            RequestedShortcut = Keybindings.ToggleOrder,
        };

    // 並び順を反転し、表示中のスレッドを組み直す（再取得はしない）。
    private void ToggleOrder()
    {
        _newestFirst = !_newestFirst;
        if (_thread is not null)
        {
            _items = BuildThreadItems(_thread);
        }

        RaiseItemsChanged();
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
