using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// 保存クエリを作成/編集する Adaptive Card フォーム。フィールドごとに演算子(いずれか/除外/未完了…)と
/// 複数選択の値を指定できる。候補は Redmine API から取得して動的に埋め込む。
/// Submit で <see cref="SavedQueryStore"/> に保存する。
/// </summary>
internal sealed partial class SavedQueryForm : FormContent
{
    // フィールド共通の演算子（トラッカー/担当者）。
    private static readonly (string Title, string Value)[] ListOps =
    {
        ("指定なし", ""), ("いずれか(=)", "="), ("除外(≠)", "!"),
    };

    // ステータス専用の演算子（open/closed/all を含む）。
    private static readonly (string Title, string Value)[] StatusOps =
    {
        ("未完了(open)", "o"), ("完了(closed)", "c"), ("すべて", "*"),
        ("いずれか(=)", "="), ("除外(≠)", "!"), ("指定なし", ""),
    };

    private readonly RedmineRef? _project;
    private readonly RedmineApi _api;
    private readonly SavedQueryStore _store;
    private readonly SavedQuery? _editing;

    private IReadOnlyList<RedmineRef> _trackers = Array.Empty<RedmineRef>();
    private IReadOnlyList<RedmineRef> _statuses = Array.Empty<RedmineRef>();
    private IReadOnlyList<RedmineRef> _members = Array.Empty<RedmineRef>();
    private bool _loaded;

    public SavedQueryForm(RedmineRef? project, RedmineApi api, SavedQueryStore store, SavedQuery? editing)
    {
        _project = project;
        _api = api;
        _store = store;
        _editing = editing;
        TemplateJson = BuildLoadingCard();
    }

    public async System.Threading.Tasks.Task EnsureLoadedAsync()
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
            // 取得できた範囲でフォームを表示する。
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
            name = _project?.Name ?? "保存クエリ";
        }

        var query = _editing ?? new SavedQuery();
        query.Name = name;
        query.Mode = Get("mode") == "count" ? "count" : "list";
        query.ProjectId = _project?.Id;
        query.ProjectName = _project?.Name;
        query.Tracker = ReadCondition(Get, "tracker", _trackers, assignee: false);
        query.Status = ReadCondition(Get, "status", _statuses, assignee: false);
        query.Assignee = ReadCondition(Get, "assignee", _members, assignee: true);

        _store.AddOrUpdate(query);
        return CommandResult.GoHome();
    }

    private static FilterCondition ReadCondition(
        Func<string, string> get, string field, IReadOnlyList<RedmineRef> options, bool assignee)
    {
        var op = get($"{field}_op");
        var condition = new FilterCondition { Op = op };

        if (op is "=" or "!")
        {
            var raw = get($"{field}_vals");
            foreach (var value in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                condition.Values.Add(new FilterValue { Value = value, Name = ValueName(value, options, assignee) });
            }
        }

        return condition;
    }

    private static string ValueName(string value, IReadOnlyList<RedmineRef> options, bool assignee)
    {
        if (assignee && value == "me")
        {
            return "自分";
        }

        return int.TryParse(value, out var id)
            ? options.FirstOrDefault(o => o.Id == id)?.Name ?? value
            : value;
    }

    private string BuildCard()
    {
        var trackerVals = _trackers.Select(t => (t.Name, t.Id.ToString(CultureInfo.InvariantCulture)));
        var statusVals = _statuses.Select(s => (s.Name, s.Id.ToString(CultureInfo.InvariantCulture)));
        var assigneeVals = new List<(string, string)> { ("自分", "me") };
        assigneeVals.AddRange(_members.Select(m => (m.Name, m.Id.ToString(CultureInfo.InvariantCulture))));

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"size\":\"Medium\",\"weight\":\"Bolder\",\"text\":{J(_project?.Name ?? "全プロジェクト")}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"name\",\"label\":\"名前\",\"isRequired\":true,\"errorMessage\":\"名前は必須です\",\"value\":{J(_editing?.Name ?? _project?.Name ?? string.Empty)}}},");
        sb.Append(ChoiceSet("mode", "モード", new[] { ("一覧", "list"), ("件数", "count") }, _editing?.Mode ?? "list", isMulti: false));
        sb.Append(',');
        sb.Append(FieldBlock("tracker", "トラッカー", ListOps, ConditionOp(_editing?.Tracker), trackerVals, ConditionVals(_editing?.Tracker)));
        sb.Append(',');
        sb.Append(FieldBlock("status", "ステータス", StatusOps, ConditionOp(_editing?.Status, "o"), statusVals, ConditionVals(_editing?.Status)));
        sb.Append(',');
        sb.Append(FieldBlock("assignee", "担当者", ListOps, ConditionOp(_editing?.Assignee), assigneeVals, ConditionVals(_editing?.Assignee)));
        sb.Append("],\"actions\":[{\"type\":\"Action.Submit\",\"title\":\"保存\"}]}");
        return sb.ToString();
    }

    // 1 フィールド = 演算子(auto) + 値(stretch) の 2 カラム。
    private static string FieldBlock(
        string field, string label,
        IEnumerable<(string Title, string Value)> opChoices, string opValue,
        IEnumerable<(string Title, string Value)> valueChoices, string valueValue)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"ColumnSet\",\"columns\":[");
        sb.Append("{\"type\":\"Column\",\"width\":\"auto\",\"items\":[");
        sb.Append(ChoiceSet($"{field}_op", label, opChoices, opValue, isMulti: false));
        sb.Append("]},");
        sb.Append("{\"type\":\"Column\",\"width\":\"stretch\",\"items\":[");
        sb.Append(ChoiceSet($"{field}_vals", "値（複数選択可）", valueChoices, valueValue, isMulti: true));
        sb.Append("]}");
        sb.Append("]}");
        return sb.ToString();
    }

    private static string BuildLoadingCard() =>
        "{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[{\"type\":\"TextBlock\",\"text\":\"読み込み中…\"}]}";

    private static string ChoiceSet(
        string id, string label, IEnumerable<(string Title, string Value)> choices, string value, bool isMulti)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.ChoiceSet\",\"id\":{J(id)},\"label\":{J(label)},\"value\":{J(value)},\"isMultiSelect\":{(isMulti ? "true" : "false")},\"choices\":[");
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

    private static string ConditionOp(FilterCondition? c, string fallback = "") => c?.Op ?? fallback;

    private static string ConditionVals(FilterCondition? c) =>
        c is null ? string.Empty : string.Join(",", c.Values.Select(v => v.Value));

    // JSON 文字列としてエスケープして引用符で囲む(非 ASCII も \\u 形式に。AOT 安全)。
    private static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>保存クエリフォームをホストする ContentPage。表示時に候補を取得する。</summary>
internal sealed partial class SavedQueryFormPage : ContentPage
{
    private readonly SavedQueryForm _form;
    private bool _started;

    public SavedQueryFormPage(RedmineRef? project, RedmineApi api, SavedQueryStore store, SavedQuery? editing = null)
    {
        Title = editing is null ? "保存クエリを作成" : "保存クエリを編集";
        Name = editing is null ? "作成" : "編集";
        Icon = new IconInfo(""); // glyph:E710
        _form = new SavedQueryForm(project, api, store, editing);
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
