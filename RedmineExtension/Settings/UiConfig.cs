using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedmineExtension;

/// <summary>
/// 表示と操作のカスタマイズ（詳細ペイン既定項目・新規クエリの固定既定・キーバインド上書き）。
/// 接続系の設定（URL/キー等）とは別に LocalState の ui-config.json に永続化する。秘密情報は含めない。
/// </summary>
internal sealed class UiConfig
{
    /// <summary>詳細ペインの既定表示項目（TicketDetails.Fields のキー）。null=全項目。</summary>
    public List<string>? DetailFields { get; set; }

    /// <summary>新しい保存クエリを既定でトップレベルに固定するか。</summary>
    public bool PinNewQueries { get; set; }

    /// <summary>アクション id → "Ctrl+Shift+K" 形式のキーバインド上書き。未指定は既定。</summary>
    public Dictionary<string, string>? Keybindings { get; set; }
}

// source-gen により反射なしで直列/逆直列化 → AOT/トリミング安全。
[JsonSerializable(typeof(UiConfig))]
internal sealed partial class UiConfigJsonContext : JsonSerializerContext
{
}

/// <summary>ui-config.json の読み書き。LocalState が無い環境（unpackaged 実行）ではメモリのみ。</summary>
internal sealed class UiConfigStore
{
    private readonly object _lock = new();
    private readonly string? _filePath;
    private UiConfig _config;

    public UiConfigStore()
    {
        _filePath = TryGetFilePath();
        _config = Load();
    }

    /// <summary>カスタマイズ保存時に発火。表示側は次回構築時に反映する。</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<string>? DetailFields
    {
        get
        {
            lock (_lock)
            {
                return _config.DetailFields;
            }
        }
    }

    public bool PinNewQueries
    {
        get
        {
            lock (_lock)
            {
                return _config.PinNewQueries;
            }
        }
    }

    /// <summary>アクション id の上書きバインド（"Ctrl+K" 等）。未設定なら null。</summary>
    public string? KeybindingOverride(string actionId)
    {
        lock (_lock)
        {
            return _config.Keybindings is { } map && map.TryGetValue(actionId, out var value) ? value : null;
        }
    }

    /// <summary>カスタマイズ一式を保存する（フォームから）。</summary>
    public void Save(List<string>? detailFields, bool pinNewQueries, Dictionary<string, string>? keybindings)
    {
        lock (_lock)
        {
            _config = new UiConfig
            {
                DetailFields = detailFields,
                PinNewQueries = pinNewQueries,
                Keybindings = keybindings is { Count: > 0 } ? keybindings : null,
            };

            Persist();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private UiConfig Load()
    {
        try
        {
            if (_filePath is not null && File.Exists(_filePath))
            {
                using var stream = File.OpenRead(_filePath);
                var config = JsonSerializer.Deserialize(stream, UiConfigJsonContext.Default.UiConfig);
                if (config is not null)
                {
                    return config;
                }
            }
        }
        catch
        {
            // 壊れたファイルは無視して既定で始める。
        }

        return new UiConfig();
    }

    private void Persist()
    {
        if (_filePath is null)
        {
            return;
        }

        try
        {
            using var stream = File.Create(_filePath);
            JsonSerializer.Serialize(stream, _config, UiConfigJsonContext.Default.UiConfig);
        }
        catch
        {
            // 保存失敗は致命的でないため無視。
        }
    }

    private static string? TryGetFilePath()
    {
        try
        {
            var dir = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            return Path.Combine(dir, "ui-config.json");
        }
        catch
        {
            return null;
        }
    }
}
