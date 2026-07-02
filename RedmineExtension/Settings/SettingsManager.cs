using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// 接続系の設定を管理する。トーキットの <see cref="Settings"/> を内包し、
/// 型付きアクセサを公開する(add-extension-settings スキル準拠)。
/// 設定ページは接続に必要な最小限（URL / API キー / 履歴件数 / 件数TTL）に留め、
/// 表示・キーバインドのカスタマイズは <see cref="UiConfigStore"/>（カスタマイズフォーム）が持つ。
/// API キーのみ設定 JSON ではなく Windows 資格情報マネージャに保存する。
/// </summary>
internal sealed class SettingsManager
{
    private const string ServerUrlKey = "redmineServerUrl";
    private const string ApiKeyKey = "redmineApiKey";
    private const string HistoryCountKey = "historyCount";
    private const string CountTtlKey = "countTtlMinutes";

    private readonly string _apiKeyCredentialTarget = "RedmineExtension/ApiKey";
    private readonly int _defaultHistoryCount = 10;
    private readonly int _maxHistoryCount = 50;

    private readonly Settings _settings = new();
    private readonly TextSetting _apiKeySetting;
    private readonly ChoiceSetSetting _historyCountSetting;
    private readonly ChoiceSetSetting _countTtlSetting;
    private readonly UiConfigStore _uiConfig;

    public SettingsManager(UiConfigStore uiConfig)
    {
        _uiConfig = uiConfig;

        _settings.Add(new TextSetting(ServerUrlKey, string.Empty)
        {
            Label = Strings.SettingsUi.ServerUrlLabel,
            Description = Strings.SettingsUi.ServerUrlDescription,
            Placeholder = "http://redmine/example",
            IsRequired = true,
        });

        _apiKeySetting = new TextSetting(ApiKeyKey, string.Empty)
        {
            Label = Strings.SettingsUi.ApiKeyLabel,
            Description = Strings.SettingsUi.ApiKeyDescription,
            Placeholder = Strings.SettingsUi.ApiKeyPlaceholder,
        };
        _settings.Add(_apiKeySetting);

        // 端数に意味が薄い数値はコンボボックスで選択肢を提示する。
        _historyCountSetting = new ChoiceSetSetting(HistoryCountKey, Choices(
            (Strings.SettingsUi.HistoryCountNone, "0"),
            (Strings.SettingsUi.HistoryCountItem(5), "5"),
            (Strings.SettingsUi.HistoryCountItem(10), "10"),
            (Strings.SettingsUi.HistoryCountItem(20), "20"),
            (Strings.SettingsUi.HistoryCountItem(30), "30"),
            (Strings.SettingsUi.HistoryCountItem(50), "50")))
        {
            Label = Strings.SettingsUi.HistoryCountLabel,
            Description = Strings.SettingsUi.HistoryCountDescription,
        };
        _settings.Add(_historyCountSetting);

        _countTtlSetting = new ChoiceSetSetting(CountTtlKey, Choices(
            (Strings.SettingsUi.Minutes(5), "5"),
            (Strings.SettingsUi.Minutes(15), "15"),
            (Strings.SettingsUi.Minutes(30), "30"),
            (Strings.SettingsUi.Hours(1), "60"),
            (Strings.SettingsUi.Hours(3), "180")))
        {
            Label = Strings.SettingsUi.CountTtlLabel,
            Description = Strings.SettingsUi.CountTtlDescription,
        };
        _settings.Add(_countTtlSetting);

        _settings.SettingsChanged += OnSettingsChanged;
    }

    private static List<ChoiceSetSetting.Choice> Choices(params (string Title, string Value)[] entries)
    {
        var list = new List<ChoiceSetSetting.Choice>(entries.Length);
        foreach (var (title, value) in entries)
        {
            list.Add(new ChoiceSetSetting.Choice(title, value));
        }

        return list;
    }

    /// <summary>CommandProvider の Settings に渡す。設定ページは自動生成される。</summary>
    public ICommandSettings Settings => _settings;

    /// <summary>未設定時に設定ページへ誘導する一覧項目（Enter で設定を開く）。</summary>
    public IListItem SettingsPrompt() =>
        new ListItem(_settings.SettingsPage)
        {
            Title = Strings.Setup.RequiredTitle,
            Subtitle = Strings.Setup.RequiredSubtitle,
            Icon = new IconInfo(""), // glyph:E713
        };

    /// <summary>自動生成された設定ページ。未設定時の誘導などからナビゲートに使う。</summary>
    public IContentPage SettingsPage => _settings.SettingsPage;

    public string ServerUrl =>
        (_settings.GetSetting<string>(ServerUrlKey) ?? string.Empty).Trim().TrimEnd('/');

    // API キーは資格情報マネージャから取得する。
    public string ApiKey =>
        (CredentialStore.Read(_apiKeyCredentialTarget) ?? string.Empty).Trim();

    /// <summary>履歴の表示件数(0〜MaxHistoryRetained にクランプ)。</summary>
    public int HistoryCount =>
        int.TryParse(_historyCountSetting.Value, out var value)
            ? Math.Clamp(value, 0, _maxHistoryCount)
            : _defaultHistoryCount;

    /// <summary>履歴として保持する最大件数(表示件数を増やしても遡れるよう余裕を持つ)。</summary>
    public int MaxHistoryRetained => _maxHistoryCount;

    /// <summary>保存クエリ件数の鮮度(TTL)。これより古い記録はハブを開いた際に再取得する。</summary>
    public TimeSpan CountTtl =>
        TimeSpan.FromMinutes(int.TryParse(_countTtlSetting.Value, out var minutes) && minutes > 0 ? minutes : 30);

    /// <summary>新しい保存クエリを既定でトップレベルに固定するか（カスタマイズで変更）。</summary>
    public bool PinNewQueriesByDefault => _uiConfig.PinNewQueries;

    /// <summary>詳細ペインに表示する項目キーの既定値（カスタマイズで変更。未設定=全項目）。</summary>
    public IReadOnlyCollection<string> DefaultDetailFields =>
        _uiConfig.DetailFields is { } fields
            ? fields.ToHashSet()
            : TicketDetails.Fields.Select(f => f.Key).ToHashSet();

    private void OnSettingsChanged(object sender, Settings args)
    {
        // 入力された API キーを資格情報マネージャへ移し、平文を入力欄に残さない。
        var entered = _apiKeySetting.Value;
        if (!string.IsNullOrWhiteSpace(entered))
        {
            CredentialStore.Save(_apiKeyCredentialTarget, entered.Trim());
            _apiKeySetting.Value = string.Empty;
        }
    }
}
