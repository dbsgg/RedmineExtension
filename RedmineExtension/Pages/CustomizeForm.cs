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
/// 表示と操作のカスタマイズフォーム。詳細ペインの既定表示項目（複数選択）、
/// 新規保存クエリの固定既定、全アクションのキーバインドを1画面で編集し
/// <see cref="UiConfigStore"/> に保存する。接続系の設定は CmdPal 標準の設定ページ側。
/// </summary>
internal sealed partial class CustomizeForm : FormContent
{
    private readonly UiConfigStore _config;
    private readonly SettingsManager _settings;

    public CustomizeForm(UiConfigStore config, SettingsManager settings)
    {
        _config = config;
        _settings = settings;
        TemplateJson = BuildCard();
    }

    public override CommandResult SubmitForm(string inputs)
    {
        using var doc = JsonDocument.Parse(inputs);
        var root = doc.RootElement;

        string Get(string key) => root.TryGetProperty(key, out var v) ? (v.GetString() ?? string.Empty) : string.Empty;

        // --- キーバインド: 全行を検証（不正・重複があれば保存せずトーストで指摘） ---
        var bindings = new Dictionary<string, string>();
        var used = new HashSet<string>();
        foreach (var action in Keybindings.Actions)
        {
            var text = Get("key_" + action.Id).Trim();
            if (text.Length == 0)
            {
                text = action.DefaultBinding; // 空欄は既定に戻す
            }

            if (!Keybindings.TryParse(text, out var chord))
            {
                return CommandResult.ShowToast(Strings.Customize.InvalidBinding(action.Label(), text));
            }

            // 表記ゆれ（Ctrl+shift+k 等）を吸収するため解析結果で重複判定する。
            if (!used.Add($"{chord.Modifiers}:{chord.Vkey}"))
            {
                return CommandResult.ShowToast(Strings.Customize.DuplicateBinding(text));
            }

            if (!string.Equals(text, action.DefaultBinding, StringComparison.OrdinalIgnoreCase))
            {
                bindings[action.Id] = text;
            }
        }

        // --- 詳細ペインの既定項目: 全選択なら null（=既定のまま全項目）として保存 ---
        var selected = Get("detailFields")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var detailFields = selected.Count == TicketDetails.Fields.Length ? null : selected;

        _config.Save(detailFields, Get("pinNew") == "true", Get("commentsOldest") == "true", bindings);

        BackgroundJob.Notify(Strings.Customize.Saved);
        return CommandResult.GoBack();
    }

    private string BuildCard()
    {
        var currentFields = _settings.DefaultDetailFields;

        var sb = new StringBuilder();
        sb.Append("{\"type\":\"AdaptiveCard\",\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"version\":\"1.5\",\"body\":[");

        // --- セクション1: 詳細ペインの既定項目（見出し + 開閉ボタン + 複数選択） ---
        // カスタマイズ済みなら初期状態で開いておく。畳んだまま保存しても選択は維持される。
        var fieldsCustomized = _config.DetailFields is not null;
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"wrap\":true,\"weight\":\"Bolder\",\"text\":{J(Strings.Customize.DetailFieldsLabel)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"ActionSet\",\"actions\":[{{\"type\":\"Action.ToggleVisibility\",\"title\":{J(Strings.Customize.ShowDetailFields)},\"targetElements\":[\"detailFieldsBox\"]}}]}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Container\",\"id\":\"detailFieldsBox\",\"isVisible\":{(fieldsCustomized ? "true" : "false")},\"items\":[");
        sb.Append("{\"type\":\"Input.ChoiceSet\",\"id\":\"detailFields\",\"isMultiSelect\":true,\"choices\":[");
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

        sb.Append(CultureInfo.InvariantCulture, $"],\"value\":{J(string.Join(",", currentFields))}}}]}},");

        // --- セクション2: 表示と動作（見出し + コメント既定順 + 固定既定トグル） ---
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"wrap\":true,\"weight\":\"Bolder\",\"spacing\":\"Large\",\"separator\":true,\"text\":{J(Strings.Customize.BehaviorHeader)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Toggle\",\"id\":\"commentsOldest\",\"title\":{J(Strings.Customize.CommentsOldestToggle)},\"value\":{J(_config.CommentsNewestFirst ? "false" : "true")}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Toggle\",\"id\":\"pinNew\",\"title\":{J(Strings.SettingsUi.PinNewLabel)},\"value\":{J(_config.PinNewQueries ? "true" : "false")}}},");

        // --- セクション3: キーバインド（見出し + 開閉ボタン + 1 アクション 1 行。空欄=既定に戻す） ---
        // 上書きが1つでもあれば初期状態で開いておく。非表示中の入力値も送信されるため、
        // 畳んだまま保存しても現在の設定は維持される。
        var anyCustom = Keybindings.Actions.Any(a => Keybindings.BindingText(a.Id) != a.DefaultBinding);
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"wrap\":true,\"spacing\":\"Large\",\"separator\":true,\"weight\":\"Bolder\",\"text\":{J(Strings.Customize.KeybindingsHeader)}}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"ActionSet\",\"actions\":[{{\"type\":\"Action.ToggleVisibility\",\"title\":{J(Strings.Customize.ShowKeybindings)},\"targetElements\":[\"keybindingsBox\"]}}]}},");
        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Container\",\"id\":\"keybindingsBox\",\"isVisible\":{(anyCustom ? "true" : "false")},\"items\":[");
        var firstKey = true;
        foreach (var action in Keybindings.Actions)
        {
            if (!firstKey)
            {
                sb.Append(',');
            }

            firstKey = false;
            sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"Input.Text\",\"id\":{J("key_" + action.Id)},\"label\":{J(action.Label())},\"placeholder\":{J(action.DefaultBinding)},\"value\":{J(Keybindings.BindingText(action.Id))}}}");
        }

        sb.Append("]},");

        sb.Append(CultureInfo.InvariantCulture, $"{{\"type\":\"TextBlock\",\"isSubtle\":true,\"wrap\":true,\"text\":{J(Strings.Customize.SaveHint)}}}");
        sb.Append(CultureInfo.InvariantCulture, $"],\"actions\":[{{\"type\":\"Action.Submit\",\"title\":{J(Strings.Customize.Submit)}}}]}}");
        return sb.ToString();
    }

    // JSON 文字列としてエスケープして引用符で囲む(非 ASCII も \u 形式に。AOT 安全)。
    private static string J(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";
}

/// <summary>カスタマイズフォームをホストする ContentPage。</summary>
internal sealed partial class CustomizePage : ContentPage
{
    private readonly UiConfigStore _config;
    private readonly SettingsManager _settings;

    public CustomizePage(UiConfigStore config, SettingsManager settings)
    {
        _config = config;
        _settings = settings;
        Id = "redmine-customize"; // 安定 Id（top-level キャッシュの重複排除用）
        Title = Strings.Customize.PageTitle;
        Name = Strings.Customize.CommandName;
        Icon = new IconInfo(""); // glyph:E771
    }

    // 開く度に現在値でフォームを作り直す（前回保存分を反映）。
    public override IContent[] GetContent() => [new CustomizeForm(_config, _settings)];
}
