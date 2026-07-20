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
/// 保存クエリの作成/編集フォーム（貼り付け方式）。名前と、Redmine のクエリ
/// （query_id=NN / "status_id=open&amp;..." / 完全な URL）を1つ貼り付けて保存する。
/// トップレベル固定と詳細ペイン表示項目のローカル設定もここで行う（既定値は拡張設定）。
/// API キーは含めない（資格情報マネージャからヘッダ付与）。
/// </summary>
internal sealed partial class SavedQueryForm : FormContent
{
    private readonly SavedQueryStore _store;
    private readonly SavedQuery? _editing;

    // 「詳細ペインをこのクエリ専用に設定するか」は切替ボタン（Action.Submit +
    // data マーカー）で反転する内部フラグで持つ。Adaptive Card には入力値に連動した
    // 表示切替が無いため、押下のたびにカードを作り直して TemplateJson を差し替える。
    // 作り直しをまたいで入力値を失わないよう、直前の値を下のフィールドに控える。
    private bool _detailsCustom;
    private string _name;
    private string _raw;
    private bool _pinned;
    private List<string> _selectedFields;

    public SavedQueryForm(SavedQueryStore store, SavedQuery? editing, SettingsManager settings)
    {
        _store = store;
        _editing = editing;
        _detailsCustom = editing?.DetailFields is not null;
        _name = editing?.Name ?? string.Empty;
        _raw = editing?.RawQuery ?? string.Empty;
        _pinned = editing?.PinnedToTopLevel ?? settings.PinNewQueriesByDefault;
        _selectedFields = editing?.DetailFields?.ToList() ?? settings.DefaultDetailFields.ToList();
        TemplateJson = BuildCard();
    }

    public override CommandResult SubmitForm(string inputs, string data)
    {
        using var doc = JsonDocument.Parse(inputs);
        var root = doc.RootElement;

        string Get(string key) => root.TryGetProperty(key, out var v) ? (v.GetString() ?? string.Empty) : string.Empty;

        // どのボタンでも、まず現在の入力値を控える（切替でカードを作り直しても失われない）。
        _name = Get("name").Trim();
        _raw = Get("query");
        _pinned = Get("pinned") == "true";
        if (_detailsCustom)
        {
            _selectedFields = Get("detailFields")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        // 切替ボタン：保存せず内部フラグを反転し、カードを差し替えてフォームに留まる。
        if (IsToggleDetailsAction(data))
        {
            _detailsCustom = !_detailsCustom;
            TemplateJson = BuildCard();
            return CommandResult.KeepOpen();
        }

        var query = _editing ?? new SavedQuery();

        // 名前は必須にしない（必須だと切替ボタンの送信まで塞がる）。空なら既定名を補う。
        query.Name = string.IsNullOrEmpty(_name) ? Strings.Queries.FormDefaultName : _name;
        query.PinnedToTopLevel = _pinned;

        // 貼り付けに key= が含まれていても永続化ファイル・画面表示へ残さない（ハードルール）。
        var raw = RedmineApi.StripApiKey(_raw);
        query.RawQuery = string.IsNullOrEmpty(raw) ? null : raw;

        // 詳細ペインのローカル設定。「既定に従う」なら null、専用指定なら選択キーの一覧。
        query.DetailFields = _detailsCustom ? _selectedFields.ToList() : null;

        _store.AddOrUpdate(query);
        return CommandResult.GoHome();
    }

    // 切替ボタン（data に {"formAction":"toggleDetails"}）による送信か判定する。
    private static bool IsToggleDetailsAction(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(data);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("formAction", out var v)
                && v.GetString() == "toggleDetails";
        }
        catch (JsonException)
        {
            // 保存ボタンなど data 無しの送信は空や非 JSON になり得る。
            return false;
        }
    }

    private string BuildCard()
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"name\",\"label\":{J(Strings.Queries.FormNameLabel)},\"placeholder\":{J(Strings.Queries.FormDefaultName)},\"value\":{J(_name)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":\"query\",\"label\":{J(Strings.Queries.FormQueryLabel)},\"isMultiline\":true,\"placeholder\":\"query_id=42 / status_id=open&assigned_to_id=me / https://redmine/issues?...\",\"value\":{J(_raw)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Toggle\",\"id\":\"pinned\",\"title\":{J(Strings.Queries.FormPinToggle)},\"value\":{J(_pinned ? "true" : "false")}}},");

        // 「このクエリ専用に設定する／既定に戻す」の切替ボタン。選択リストは個別
        // カスタマイズ中のみカードに含める。
        var toggleTitle = _detailsCustom ? Strings.Queries.FormDetailsRevert : Strings.Queries.FormDetailsToggle;
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"ActionSet\",\"actions\":[{{\"type\":\"Action.Submit\",\"title\":{J(toggleTitle)},\"data\":{{\"formAction\":\"toggleDetails\"}}}}]}},");

        if (_detailsCustom)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.ChoiceSet\",\"id\":\"detailFields\",\"label\":{J(Strings.Queries.FormDetailsLabel)},\"isMultiSelect\":true,\"choices\":[");
            var first = true;
            foreach (var (key, label) in TicketDetails.Fields)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                sb.Append(CultureInfo.InvariantCulture, $"{{\"title\":{J(label)},\"value\":{J(key)}}}");
            }

            sb.Append(CultureInfo.InvariantCulture, $"],\"value\":{J(string.Join(",", _selectedFields))}}},");
        }

        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"isSubtle\":true,\"wrap\":true,\"text\":{J(Strings.Queries.FormHint)}}}");
        sb.Append(CultureInfo.InvariantCulture, $"],\"actions\":[{{\"type\":\"Action.Submit\",\"title\":{J(Strings.Queries.FormSubmit)}}}]}}");
        return sb.ToString();
    }

    // JSON 文字列としてエスケープして引用符で囲む(非 ASCII も \\u 形式に。AOT 安全)。
    private static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>保存クエリフォームをホストする ContentPage。</summary>
internal sealed partial class SavedQueryFormPage : ContentPage
{
    private readonly SavedQueryForm _form;

    public SavedQueryFormPage(SavedQueryStore store, SettingsManager settings, SavedQuery? editing = null)
    {
        Title = editing is null ? Strings.Queries.FormCreateTitle : Strings.Queries.FormEditTitle;
        Name = editing is null ? Strings.Queries.FormCreateName : Strings.Queries.FormEditName;
        Icon = new IconInfo(""); // glyph:E710
        _form = new SavedQueryForm(store, editing, settings);
    }

    public override IContent[] GetContent() => [_form];
}
