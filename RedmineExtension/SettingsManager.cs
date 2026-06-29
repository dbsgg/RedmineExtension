using System;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// 拡張の設定を一元管理する。トーキットの <see cref="Settings"/> を内包し、
/// 型付きアクセサを公開する(add-extension-settings スキル準拠)。
/// API キーのみ設定 JSON ではなく Windows 資格情報マネージャに保存する。
/// </summary>
internal sealed class SettingsManager
{
    private const string ServerUrlKey = "redmineServerUrl";
    private const string ApiKeyKey = "redmineApiKey";
    private const string HistoryCountKey = "historyCount";

    private readonly string _apiKeyCredentialTarget = "RedmineExtension/ApiKey";
    private readonly int _defaultHistoryCount = 10;
    private readonly int _maxHistoryCount = 50;

    private readonly Settings _settings = new();
    private readonly TextSetting _apiKeySetting;
    private readonly TextSetting _historyCountSetting;

    public SettingsManager()
    {
        _settings.Add(new TextSetting(ServerUrlKey, string.Empty)
        {
            Label = "Redmine URL",
            Description = "例: http://redmine/example",
            Placeholder = "http://redmine/example",
            IsRequired = true,
        });

        _apiKeySetting = new TextSetting(ApiKeyKey, string.Empty)
        {
            Label = "API access key",
            Description = "Redmine の API アクセスキー。Windows 資格情報マネージャに保存され、入力後この欄は空に戻ります。",
            Placeholder = "新しいキーを入力すると更新します",
        };
        _settings.Add(_apiKeySetting);

        _historyCountSetting = new TextSetting(
            HistoryCountKey,
            _defaultHistoryCount.ToString(CultureInfo.InvariantCulture))
        {
            Label = "履歴の表示件数",
            Description = $"検索ボックスが空のときに表示する直近チケットの件数(0〜{_maxHistoryCount})。",
        };
        _settings.Add(_historyCountSetting);

        _settings.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>CommandProvider の Settings に渡す。設定ページは自動生成される。</summary>
    public ICommandSettings Settings => _settings;

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
