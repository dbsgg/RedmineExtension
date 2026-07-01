// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

internal sealed partial class RedmineExtensionPage : DynamicListPage
{
    private readonly SettingsManager _settings;
    private readonly RedmineApi _api;
    private readonly TicketHistory _history;

    // チケット項目は id 単位でキャッシュし、取得完了時に Title を更新する。
    private readonly ConcurrentDictionary<int, ListItem> _ticketItems = new();

    // id → 取得済み subject(キーがあれば取得試行済み。null/空 = タイトル無し)。
    private readonly ConcurrentDictionary<int, string?> _subjects = new();
    // id → 取得失敗時のエラー文言。
    private readonly ConcurrentDictionary<int, string> _errors = new();
    private readonly HashSet<int> _loading = new();
    private readonly object _loadLock = new();

    // id → チケット概要（副題・右ペイン詳細の元。履歴でも共有）。バッチ/単体取得で埋める。
    private readonly ConcurrentDictionary<int, IssueSummary> _summaries = new();
    private readonly HashSet<int> _detailsAttempted = new();
    private readonly HashSet<int> _detailsLoading = new();
    private readonly object _detailsLock = new();
    private bool _batchLoading;

    private string _search = string.Empty;

    public RedmineExtensionPage(SettingsManager settings, RedmineApi api, TicketHistory history)
    {
        Icon = new IconInfo("");
        Title = "Redmine";
        Name = "Open";
        PlaceholderText = "チケット番号を入力（番号の後にスペースでタイトル表示）";
        ShowDetails = true; // フォーカス時に右ペインの詳細を表示する

        _settings = settings;
        _api = api;
        _history = history;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _search = newSearch;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems() => BuildItems(_search);

    private IListItem[] BuildItems(string search)
    {
        // URL / API キーが未設定なら、まず設定ページへ誘導する。
        if (!_api.IsConfigured)
        {
            return [BuildSettingsPrompt()];
        }

        var raw = search ?? string.Empty;
        var body = raw.TrimStart();
        if (body.StartsWith('#'))
        {
            body = body[1..];
        }

        var digitCount = 0;
        while (digitCount < body.Length && char.IsDigit(body[digitCount]))
        {
            digitCount++;
        }

        var rest = body[digitCount..];
        var restIsDelimiterOrEmpty = rest.Length == 0 || char.IsWhiteSpace(rest[0]);

        if (digitCount > 0 && restIsDelimiterOrEmpty &&
            int.TryParse(body[..digitCount], out var id) && id > 0)
        {
            // 数字の後に区切り(スペース等)が入っていれば「確定」とみなしタイトルを取得する。
            var confirmed = rest.Length > 0;
            var item = _ticketItems.GetOrAdd(id, CreateTicketItem);
            if (confirmed)
            {
                EnsureTitleLoaded(id, item);
            }

            return [item];
        }

        return DefaultItems(raw);
    }

    private ListItem CreateTicketItem(int id)
    {
        var url = _api.IssueUrl(id);
        var item = new ListItem(new OpenTicketCommand(url, id, () => _subjects.GetValueOrDefault(id), _history))
        {
            Title = ComposeTitle(id),
            Subtitle = string.Empty,
            Icon = new IconInfo(""), // Globe
        };

        // 取得済みなら副題・詳細を即反映。
        if (_summaries.TryGetValue(id, out var summary))
        {
            ApplyDetail(item, summary, url);
        }

        item.MoreCommands = [CopyContext(id), RefreshContext(id, item), CommentsContext(id)];

        return item;
    }

    private static void ApplyDetail(ListItem item, IssueSummary summary, string url)
    {
        item.Subtitle = TicketDetails.Subtitle(summary);
        item.Details = TicketDetails.Build(summary, url);
    }

    private string ComposeTitle(int id)
    {
        var subject = _subjects.GetValueOrDefault(id);
        if (!string.IsNullOrEmpty(subject))
        {
            return $"#{id} {subject}";
        }

        // 取得に失敗していれば原因を表示する。
        if (_errors.TryGetValue(id, out var error))
        {
            var shortError = error.Length > 80 ? error[..80] + "…" : error;
            return $"#{id} ⚠ {shortError}";
        }

        return $"#{id} を開く";
    }

    private void EnsureTitleLoaded(int id, ListItem item)
    {
        if (_subjects.ContainsKey(id) || !_api.IsConfigured)
        {
            return;
        }

        lock (_loadLock)
        {
            if (!_loading.Add(id))
            {
                return;
            }
        }

        item.Title = $"#{id}（タイトル取得中…）";

        _ = Task.Run(async () =>
        {
            string? subject = null;
            try
            {
                var issue = await _api.GetIssueAsync(id).ConfigureAwait(false);
                subject = issue.Subject;
                _errors.TryRemove(id, out _);

                // 副題・右ペイン詳細を表示する（履歴と共有するためキャッシュ）。
                _summaries[id] = issue;
                ApplyDetail(item, issue, _api.IssueUrl(id));
            }
            catch (System.Exception ex)
            {
                _errors[id] = ex.GetType().Name + ": " + ex.Message;
            }

            // 成功・失敗とも結果を記録する。
            _subjects[id] = subject;
            lock (_loadLock)
            {
                _loading.Remove(id);
            }

            // Title 更新とリスト更新通知で反映する。
            item.Title = ComposeTitle(id);
            RaiseItemsChanged();
        });
    }

    private IListItem[] DefaultItems(string raw)
    {
        var items = new List<IListItem>();

        // 検索ボックスが空のときだけ直近の履歴を設定件数まで表示する。
        if (string.IsNullOrWhiteSpace(raw))
        {
            var entries = _history.Recent.Take(_settings.HistoryCount).ToList();
            foreach (var entry in entries)
            {
                items.Add(HistoryItem(entry));
            }

            // 履歴チケットの詳細を 1 リクエストでまとめて取得（負荷を抑える）。
            EnsureHistoryDetails(entries.Select(e => e.Id).ToList());
        }

        items.Add(new ListItem(new OpenUrlCommand(_settings.ServerUrl))
        {
            Title = "Redmine を開く",
            Subtitle = _settings.ServerUrl,
            Icon = new IconInfo(""),
        });

        return items.ToArray();
    }

    private ListItem HistoryItem(TicketHistoryEntry entry)
    {
        var url = _api.IssueUrl(entry.Id);
        var title = entry.Title;
        var label = string.IsNullOrWhiteSpace(title) ? $"#{entry.Id} を開く" : $"#{entry.Id} {title}";

        var item = new ListItem(new OpenTicketCommand(url, entry.Id, () => title, _history))
        {
            Title = label,
            Subtitle = string.Empty,
            Icon = new IconInfo(""), // History
        };

        // 取得済みなら副題・右ペイン詳細を即表示。
        if (_summaries.TryGetValue(entry.Id, out var summary))
        {
            ApplyDetail(item, summary, url);
        }

        item.MoreCommands = [CopyContext(entry.Id), RefreshContext(entry.Id, item), CommentsContext(entry.Id)];

        return item;
    }

    private CommandContextItem CopyContext(int id) =>
        new(new CopyTicketLinkCommand(_api, id, _history))
        {
            // Ctrl+Enter で「#番号 タイトル」リンクをコピー
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true, alt: false, shift: false, win: false,
                vkey: VirtualKey.Enter, scanCode: 0),
        };

    private CommandContextItem CommentsContext(int id) =>
        new(new CommentsPage(id, _api))
        {
            // Ctrl+C でコメントページを開く。
            Title = "コメント",
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true, alt: false, shift: false, win: false,
                vkey: VirtualKey.C, scanCode: 0),
        };

    private CommandContextItem RefreshContext(int id, ListItem item) =>
        new(new AnonymousCommand(() => RefreshTicket(id, item))
        {
            Name = "最新に更新",
            Icon = new IconInfo(""), // glyph:E72C
            Result = CommandResult.KeepOpen(),
        })
        {
            // Ctrl+R で最新の情報に更新（再取得）。
            RequestedShortcut = KeyChordHelpers.FromModifiers(
                ctrl: true, alt: false, shift: false, win: false,
                vkey: VirtualKey.R, scanCode: 0),
        };

    // 最新に更新（単体取得、include=journals で最新コメント込み）。キャッシュを上書きする。
    private void RefreshTicket(int id, ListItem item)
    {
        if (!_api.IsConfigured)
        {
            return;
        }

        lock (_detailsLock)
        {
            if (!_detailsLoading.Add(id))
            {
                return;
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var issue = await _api.GetIssueAsync(id).ConfigureAwait(false);
                _summaries[id] = issue;
                _subjects[id] = issue.Subject;
                ApplyDetail(item, issue, _api.IssueUrl(id));
                item.Title = string.IsNullOrEmpty(issue.Subject) ? $"#{id}" : $"#{id} {issue.Subject}";
            }
            catch
            {
                // 取得失敗は無視。
            }
            finally
            {
                lock (_detailsLock)
                {
                    _detailsLoading.Remove(id);
                }

                // RaiseItemsChanged() は呼ばない（リスト再生成でフォーカスが先頭に戻るため）。
                // Title/Subtitle/Details は同一項目の PropertyChanged でその場更新される。
            }
        });
    }

    // 履歴チケットの詳細を 1 リクエストでまとめて取得しキャッシュする。
    private void EnsureHistoryDetails(IReadOnlyList<int> ids)
    {
        if (!_api.IsConfigured)
        {
            return;
        }

        List<int> missing;
        lock (_detailsLock)
        {
            if (_batchLoading)
            {
                return;
            }

            missing = ids
                .Where(id => !_summaries.ContainsKey(id) && !_detailsAttempted.Contains(id))
                .Distinct()
                .ToList();

            if (missing.Count == 0)
            {
                return;
            }

            _batchLoading = true;
            foreach (var id in missing)
            {
                _detailsAttempted.Add(id);
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var map = await _api.GetIssuesAsync(missing).ConfigureAwait(false);
                foreach (var kv in map)
                {
                    _summaries[kv.Key] = kv.Value;
                }
            }
            catch
            {
                // 取得失敗は無視。
            }
            finally
            {
                lock (_detailsLock)
                {
                    _batchLoading = false;
                }

                RaiseItemsChanged();
            }
        });
    }

    // 未設定時に設定ページへ誘導する項目。Enter で設定を開く。
    private ListItem BuildSettingsPrompt() =>
        new ListItem(_settings.SettingsPage)
        {
            Title = "Redmine の設定が必要です",
            Subtitle = "Enter で設定を開き、URL と API キーを入力してください",
            Icon = new IconInfo(""), // glyph:E713
        };
}
