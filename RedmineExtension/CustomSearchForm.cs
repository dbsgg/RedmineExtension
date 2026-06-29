using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// カスタム検索を作成/編集する Adaptive Card フォーム。トラッカー/ステータス/担当者の
/// 候補は Redmine API から取得して動的に埋め込む。Submit で <see cref="SavedSearchStore"/> に保存。
/// </summary>
internal sealed partial class CustomSearchForm : FormContent
{
    private readonly RedmineRef? _project;
    private readonly RedmineApi _api;
    private readonly SavedSearchStore _store;
    private readonly SavedSearch? _editing;

    private IReadOnlyList<RedmineRef> _trackers = Array.Empty<RedmineRef>();
    private IReadOnlyList<RedmineRef> _statuses = Array.Empty<RedmineRef>();
    private IReadOnlyList<RedmineRef> _members = Array.Empty<RedmineRef>();
    private bool _loaded;

    public CustomSearchForm(RedmineRef? project, RedmineApi api, SavedSearchStore store, SavedSearch? editing)
    {
        _project = project;
        _api = api;
        _store = store;
        _editing = editing;
        TemplateJson = BuildLoadingCard();
    }

    /// <summary>候補を取得してフォームを組み立てる。TemplateJson 更新で再描画される。</summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        try
        {
            _trackers = await _api.GetTrackersAsync().ConfigureAwait(false);
            _statuses = await _api.GetStatusesAsync().ConfigureAwait(false);
            if (_project is not null)
            {
                _members = await _api.GetProjectMembersAsync(_project.Id).ConfigureAwait(false);
            }
        }
        catch
        {
            // 失敗時は取得できた範囲でフォームを表示する。
        }

        _loaded = true;
        TemplateJson = BuildCard();
    }

    public override CommandResult SubmitForm(string inputs)
    {
        using var doc = JsonDocument.Parse(inputs);
        var root = doc.RootElement;

        string Get(string key) => root.TryGetProperty(key, out var v) ? (v.GetString() ?? string.Empty) : string.Empty;

        var name = Get("name").Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = _project?.Name ?? "カスタム検索";
        }

        var search = _editing ?? new SavedSearch();
        search.Name = name;
        search.Mode = Get("mode") == "count" ? "count" : "list";
        search.ProjectId = _project?.Id;
        search.ProjectName = _project?.Name;

        var tracker = Get("tracker");
        search.TrackerId = int.TryParse(tracker, out var tid) ? tid : null;
        search.TrackerName = LookupName(_trackers, tracker);

        var status = Get("status");
        search.Status = string.IsNullOrEmpty(status) ? null : status;
        search.StatusName = StatusDisplay(status);

        var assignee = Get("assignee");
        search.Assignee = string.IsNullOrEmpty(assignee) ? null : assignee;
        search.AssigneeName = assignee == "me" ? "自分" : LookupName(_members, assignee);

        _store.AddOrUpdate(search);
        return CommandResult.GoHome();
    }

    private string BuildCard()
    {
        var assigneeChoices = new List<(string Title, string Value)> { ("指定なし", string.Empty), ("自分", "me") };
        assigneeChoices.AddRange(_members.Select(m => (m.Name, m.Id.ToString(CultureInfo.InvariantCulture))));

        var statusChoices = new List<(string Title, string Value)>
        {
            ("指定なし", string.Empty), ("未完了", "open"), ("完了", "closed"), ("すべて", "*"),
        };
        statusChoices.AddRange(_statuses.Select(s => (s.Name, s.Id.ToString(CultureInfo.InvariantCulture))));

        var trackerChoices = new List<(string Title, string Value)> { ("指定なし", string.Empty) };
        trackerChoices.AddRange(_trackers.Select(t => (t.Name, t.Id.ToString(CultureInfo.InvariantCulture))));

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"size\":\"Medium\",\"weight\":\"Bolder\",\"text\":{J(_project?.Name ?? "全プロジェクト")}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"name\",\"label\":\"名前\",\"isRequired\":true,\"errorMessage\":\"名前は必須です\",\"value\":{J(_editing?.Name ?? _project?.Name ?? string.Empty)}}},");
        sb.Append(ChoiceSet("mode", "モード", new[] { ("一覧", "list"), ("件数", "count") }, _editing?.Mode ?? "list"));
        sb.Append(',');
        sb.Append(ChoiceSet("tracker", "トラッカー", trackerChoices, _editing?.TrackerId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        sb.Append(',');
        sb.Append(ChoiceSet("status", "ステータス", statusChoices, _editing?.Status ?? "open"));
        sb.Append(',');
        sb.Append(ChoiceSet("assignee", "担当者", assigneeChoices, _editing?.Assignee ?? string.Empty));
        sb.Append("],\"actions\":[{\"type\":\"Action.Submit\",\"title\":\"保存\"}]}");
        return sb.ToString();
    }

    private static string BuildLoadingCard() =>
        "{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[{\"type\":\"TextBlock\",\"text\":\"読み込み中…\"}]}";

    private static string ChoiceSet(string id, string label, IEnumerable<(string Title, string Value)> choices, string value)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.ChoiceSet\",\"id\":{J(id)},\"label\":{J(label)},\"value\":{J(value)},\"choices\":[");
        var first = true;
        foreach (var (title, val) in choices)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append(CultureInfo.InvariantCulture, $"{{\"title\":{J(title)},\"value\":{J(val)}}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private static string? LookupName(IReadOnlyList<RedmineRef> refs, string idText) =>
        int.TryParse(idText, out var id) ? refs.FirstOrDefault(r => r.Id == id)?.Name : null;

    private string? StatusDisplay(string status) => status switch
    {
        "" => null,
        "open" => "未完了",
        "closed" => "完了",
        "*" => "すべて",
        _ => LookupName(_statuses, status),
    };

    // JSON 文字列としてエスケープして引用符で囲む(非 ASCII も \\u 形式に。AOT 安全)。
    private static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>カスタム検索フォームをホストする ContentPage。表示時に候補を取得する。</summary>
internal sealed partial class CustomSearchFormPage : ContentPage
{
    private readonly CustomSearchForm _form;
    private bool _started;

    public CustomSearchFormPage(RedmineRef? project, RedmineApi api, SavedSearchStore store, SavedSearch? editing = null)
    {
        Title = editing is null ? "カスタム検索を作成" : "カスタム検索を編集";
        Name = editing is null ? "作成" : "編集";
        Icon = new IconInfo(""); // glyph:E710
        _form = new CustomSearchForm(project, api, store, editing);
    }

    public override IContent[] GetContent()
    {
        if (!_started)
        {
            _started = true;
            _ = _form.EnsureLoadedAsync();
        }

        return [_form];
    }
}
