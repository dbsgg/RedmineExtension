using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RedmineExtension;

/// <summary>Redmine の id/名称ペア(ドロップダウン候補)。</summary>
internal sealed record RedmineRef(int Id, string Name);

/// <summary>検索結果のチケット概要。</summary>
internal sealed record IssueSummary(int Id, string Subject);

/// <summary>
/// Redmine REST API への最小限のアクセス。タイトル取得・候補取得・検索に使う。
/// API キー・サーバー URL は <see cref="SettingsManager"/>(実行時入力)から取得し、
/// ソースには直書きしない。
/// </summary>
internal sealed class RedmineApi
{
    private static readonly HttpClient Http = CreateClient();

    private readonly SettingsManager _settings;

    public RedmineApi(SettingsManager settings) => _settings = settings;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ServerUrl) &&
        !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public string IssueUrl(int id) => $"{_settings.ServerUrl}/issues/{id}";

    /// <summary>チケットのタイトル(subject)を非同期取得する。失敗時は例外を投げる。</summary>
    public async Task<string?> GetIssueSubjectAsync(int id)
    {
        using var doc = await GetAsync($"{_settings.ServerUrl}/issues/{id}.json").ConfigureAwait(false);
        return doc.RootElement.GetProperty("issue").GetProperty("subject").GetString();
    }

    public Task<IReadOnlyList<RedmineRef>> GetProjectsAsync() =>
        GetRefsAsync($"{_settings.ServerUrl}/projects.json?limit=100", "projects");

    public Task<IReadOnlyList<RedmineRef>> GetTrackersAsync() =>
        GetRefsAsync($"{_settings.ServerUrl}/trackers.json", "trackers");

    public Task<IReadOnlyList<RedmineRef>> GetStatusesAsync() =>
        GetRefsAsync($"{_settings.ServerUrl}/issue_statuses.json", "issue_statuses");

    /// <summary>プロジェクトのメンバー(ユーザー)を取得する。担当者候補に使う。</summary>
    public async Task<IReadOnlyList<RedmineRef>> GetProjectMembersAsync(int projectId)
    {
        using var doc = await GetAsync($"{_settings.ServerUrl}/projects/{projectId}/memberships.json?limit=100")
            .ConfigureAwait(false);

        var result = new List<RedmineRef>();
        foreach (var membership in doc.RootElement.GetProperty("memberships").EnumerateArray())
        {
            // user を持つメンバーのみ(group 割当は対象外)。
            if (membership.TryGetProperty("user", out var user))
            {
                result.Add(new RedmineRef(
                    user.GetProperty("id").GetInt32(),
                    user.GetProperty("name").GetString() ?? string.Empty));
            }
        }

        return result;
    }

    /// <summary>フィルタに合致するチケットを取得する。件数モード用に total_count も返す。</summary>
    public async Task<(IReadOnlyList<IssueSummary> Issues, int TotalCount)> SearchIssuesAsync(SavedSearch filter, int limit)
    {
        var url = $"{_settings.ServerUrl}/issues.json?{BuildIssueQuery(filter)}&limit={limit}";
        using var doc = await GetAsync(url).ConfigureAwait(false);

        var total = doc.RootElement.TryGetProperty("total_count", out var tc) ? tc.GetInt32() : 0;
        var issues = new List<IssueSummary>();
        foreach (var issue in doc.RootElement.GetProperty("issues").EnumerateArray())
        {
            issues.Add(new IssueSummary(
                issue.GetProperty("id").GetInt32(),
                issue.GetProperty("subject").GetString() ?? string.Empty));
        }

        return (issues, total);
    }

    /// <summary>件数モードのクリックで開く Redmine の Web 検索 URL。</summary>
    public string IssuesWebUrl(SavedSearch filter) =>
        $"{_settings.ServerUrl}/issues?set_filter=1&{BuildIssueQuery(filter)}";

    private static string BuildIssueQuery(SavedSearch f)
    {
        var parts = new List<string>();
        if (f.ProjectId is int p)
        {
            parts.Add($"project_id={p}");
        }

        if (f.TrackerId is int t)
        {
            parts.Add($"tracker_id={t}");
        }

        if (!string.IsNullOrEmpty(f.Status))
        {
            parts.Add($"status_id={Uri.EscapeDataString(f.Status)}");
        }

        if (!string.IsNullOrEmpty(f.Assignee))
        {
            parts.Add($"assigned_to_id={Uri.EscapeDataString(f.Assignee)}");
        }

        return string.Join("&", parts);
    }

    private async Task<IReadOnlyList<RedmineRef>> GetRefsAsync(string url, string arrayName)
    {
        using var doc = await GetAsync(url).ConfigureAwait(false);

        var result = new List<RedmineRef>();
        foreach (var element in doc.RootElement.GetProperty(arrayName).EnumerateArray())
        {
            result.Add(new RedmineRef(
                element.GetProperty("id").GetInt32(),
                element.GetProperty("name").GetString() ?? string.Empty));
        }

        return result;
    }

    // 反射なしの JsonDocument を返す共通 GET(AOT/トリミング安全)。呼び出し側で using で破棄する。
    private async Task<JsonDocument> GetAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Redmine-API-Key", _settings.ApiKey);

        using var response = await Http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }
}
