using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension.Pages
{
    internal sealed class RedmineSettingsPage : ContentPage
    {
        internal const string ServerUrlSettingKey = "redmineServerUrl";
        internal const string ApiKeySettingKey = "redmineApiKey";

        // 資格情報マネージャ上のターゲット名(汎用資格情報)。
        private readonly string _apiKeyCredentialTarget = "RedmineExtension/ApiKey";

        private readonly Settings _settings = new();
        private readonly TextSetting _apiKeySetting;

        public RedmineSettingsPage()
        {
            Name = "Settings";
            Icon = new IconInfo("");
            Title = "Redmine Settings";

            _settings.Add(new TextSetting(
                ServerUrlSettingKey,
                "http://localhost:8080")
            {
                Label = "Redmine URL",
                Description = "例: http://redmine/example",
                Placeholder = "http://redmine/example",
                IsRequired = true,
            });

            _apiKeySetting = new TextSetting(ApiKeySettingKey, string.Empty)
            {
                Label = "API access key",
                Description = "Redmine の API アクセスキー。Windows 資格情報マネージャに保存され、入力後この欄は空に戻ります。",
                Placeholder = "新しいキーを入力すると更新します",
            };
            _settings.Add(_apiKeySetting);

            _settings.SettingsChanged += OnSettingsChanged;
        }

        internal string ServerUrl =>
            _settings.GetSetting<string>(ServerUrlSettingKey)
                .Trim()
                .TrimEnd('/');

        // API キーは設定 JSON ではなく資格情報マネージャから取得する。
        internal string ApiKey =>
            (CredentialStore.Read(_apiKeyCredentialTarget) ?? string.Empty).Trim();

        public override IContent[] GetContent()
        {
            return _settings.ToContent();
        }

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
}
