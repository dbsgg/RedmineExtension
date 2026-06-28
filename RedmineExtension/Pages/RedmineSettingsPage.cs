using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedmineExtension.Pages
{
    internal sealed class RedmineSettingsPage : ContentPage
    {
        internal const string ServerUrlSettingKey = "redmineServerUrl";
        internal const string ApiKeySettingKey = "redmineApiKey";

        private readonly Settings _settings = new();

        public RedmineSettingsPage()
        {
            Name = "Settings";
            Icon = new IconInfo("\uE713");
            Title = "Redmine Settings";

            _settings.Add(new TextSetting(
                ServerUrlSettingKey,
                "http://localhost:8080")
            {
                Label = "Redmine URL",
                Description = "例: http://localhost:8080",
                Placeholder = "http://localhost:8080",
                IsRequired = true,
            });

            _settings.Add(new TextSetting(
                ApiKeySettingKey,
                string.Empty)
            {
                Label = "API access key",
                Description = "Redmine の個人設定で発行した API アクセスキー。",
                IsRequired = true,
            });
        }

        internal string ServerUrl =>
            _settings.GetSetting<string>(ServerUrlSettingKey)
                .Trim()
                .TrimEnd('/');

        internal string ApiKey =>
            _settings.GetSetting<string>(ApiKeySettingKey)
                .Trim();

        public override IContent[] GetContent()
        {
            return _settings.ToContent();
        }
    }
    }
