using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>クエリに合致するチケットを取得する。件数モード用に total_count も返す。</summary>
    public async Task<(IReadOnlyList<IssueSummary> Issues, int TotalCount)> SearchIssuesAsync(SavedQuery query, int limit)
    {
        var url = $"{_settings.ServerUrl}/issues.json?{EffectiveQuery(query)}&limit={limit}";
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
    public string IssuesWebUrl(SavedQuery query) =>
        $"{_settings.ServerUrl}/issues?{EffectiveQuery(query)}";

    // RawQuery があればそれを正規化して使い、無ければフィールド条件から組み立てる。
    private static string EffectiveQuery(SavedQuery query) =>
        string.IsNullOrWhiteSpace(query.RawQuery) ? BuildIssueQuery(query) : NormalizeRawQuery(query.RawQuery);

    // 貼り付けられた query_id / クエリ文字列 / URL を Redmine 用クエリへ正規化する。
    // API キー(key=)は資格情報マネージャから付与するため除去する。
    private static string NormalizeRawQuery(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            return "set_filter=1";
        }

        // 数字のみなら保存クエリ ID とみなす。
        if (raw.All(char.IsDigit))
        {
            return $"query_id={raw}";
        }

        // URL なら '?' 以降のクエリ部だけ使う。
        var questionMark = raw.IndexOf('?');
        var query = questionMark >= 0 ? raw[(questionMark + 1)..] : raw;

        var parts = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => !p.StartsWith("key=", StringComparison.OrdinalIgnoreCase));

        return string.Join("&", parts);
    }

    // Redmine の長形式フィルタ(set_filter=1 + f[]/op[]/v[])を組み立てる。
    private static string BuildIssueQuery(SavedQuery q)
    {
        var parts = new List<string> { "set_filter=1" };

        if (q.ProjectId is int projectId)
        {
            parts.Add($"project_id={projectId}");
        }

        AppendCondition(parts, "tracker_id", q.Tracker);
        AppendCondition(parts, "status_id", q.Status);
        AppendCondition(parts, "assigned_to_id", q.Assignee);

        return string.Join("&", parts);
    }

    private static void AppendCondition(List<string> parts, string field, FilterCondition condition)
    {
        if (condition is null || !condition.HasFilter)
        {
            return;
        }

        // "=" / "!" は値が必要。値が無ければ条件を付けない。
        if (condition.UsesValues && condition.Values.Count == 0)
        {
            return;
        }

        parts.Add($"f[]={field}");
        parts.Add($"op[{field}]={Uri.EscapeDataString(condition.Op)}");

        if (condition.UsesValues)
        {
            foreach (var value in condition.Values)
            {
                parts.Add($"v[{field}][]={Uri.EscapeDataString(value.Value)}");
            }
        }
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
