using Microsoft.CommandPalette.Extensions;
using RedmineExtension.Pages;
using System;
using System.Collections.Generic;
using System.Text;

namespace RedmineExtension
{
    internal sealed class RedmineCommandSettings : ICommandSettings
    {
        private RedmineSettingsPage _settingsPage = new();
        public IContentPage SettingsPage => _settingsPage;

        public string ServerUrl =>
            _settingsPage.ServerUrl;
        public string ApiKey =>
            _settingsPage.ApiKey;
    }
}
