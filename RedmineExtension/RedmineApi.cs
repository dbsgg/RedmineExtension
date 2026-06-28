using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RedmineExtension;

/// <summary>
/// Redmine REST API への最小限のアクセス。チケットのタイトル(subject)取得に使う。
/// API キー・サーバー URL は <see cref="RedmineCommandSettings"/>(実行時入力)から取得し、
/// ソースには直書きしない。
/// </summary>
internal sealed class RedmineApi
{
    private static readonly HttpClient Http = CreateClient();

    private readonly RedmineCommandSettings _settings;

    public RedmineApi(RedmineCommandSettings settings) => _settings = settings;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.ServerUrl) &&
        !string.IsNullOrWhiteSpace(_settings.ApiKey);

    public string IssueUrl(int id) => $"{_settings.ServerUrl}/issues/{id}";

    /// <summary>チケットのタイトル(subject)を非同期取得する。失敗時は例外を投げる。</summary>
    public async Task<string?> GetIssueSubjectAsync(int id)
    {
        var requestUrl = $"{_settings.ServerUrl}/issues/{id}.json";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("X-Redmine-API-Key", _settings.ApiKey);

        using var response = await Http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        // JsonDocument は反射を使わないため AOT/トリミングに安全。
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        return doc.RootElement.GetProperty("issue").GetProperty("subject").GetString();
    }

    private static HttpClient CreateClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }
}
