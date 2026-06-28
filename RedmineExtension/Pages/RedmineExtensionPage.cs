// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using RedmineExtension.Pages;

namespace RedmineExtension;

internal sealed partial class RedmineExtensionPage : ListPage
{
    private readonly RedmineCommandSettings _settings;

    public RedmineExtensionPage(RedmineCommandSettings settings)
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "Redmine";
        Name = "Open";

        _settings = settings;
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new OpenUrlCommand(_settings.ServerUrl)) { Title = "Redmineを開く" },
        ];
        
    }
}
