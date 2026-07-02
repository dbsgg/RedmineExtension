using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace RedmineExtension;

/// <summary>
/// キーバインドカタログ。ショートカットは必ずここで定義し、ページ側で直接
/// KeyChordHelpers を呼ばない（一覧性と衝突回避のため）。
/// 全アクションが「表示と操作のカスタマイズ」フォームから "Ctrl+Shift+K" 形式で
/// 上書きできる（<see cref="UiConfigStore"/> に永続化。修飾キー必須）。
/// Enter=ページ遷移は CmdPal 既定のためここには載らない。Ctrl+A は検索ボックスの
/// 全選択、素の Delete は文字削除と衝突するため既定では使わない。
/// </summary>
internal static class Keybindings
{
    /// <summary>カスタマイズ可能なアクション（id・既定バインド・表示名）。フォームの行にもなる。</summary>
    public sealed record ActionDef(string Id, string DefaultBinding, Func<string> Label);

    /// <summary>区分順（ナビゲーション → チケット操作 → 保存クエリ管理）。</summary>
    public static readonly ActionDef[] Actions =
    [
        new("openInBrowser", "Ctrl+Enter", () => Strings.Common.OpenInBrowser),
        new("back", "Alt+Left", () => Strings.Common.Back),
        new("home", "Alt+Home", () => Strings.Common.Home),
        new("copyLink", "Ctrl+C", () => Strings.Common.CopyLink),
        new("refresh", "Ctrl+R", () => Strings.Common.Refresh),
        new("loadMore", "Ctrl+L", () => Strings.Queries.LoadMore),
        new("toggleOrder", "Ctrl+O", () => Strings.Customize.ToggleOrderLabel),
        new("changeStatus", "Ctrl+S", () => Strings.QuickEdit.ChangeStatusName),
        new("addComment", "Ctrl+M", () => Strings.QuickEdit.AddCommentName),
        new("addQuery", "Ctrl+N", () => Strings.Queries.AddTitle),
        new("editQuery", "Ctrl+E", () => Strings.Customize.EditQueryLabel),
        new("deleteQuery", "Ctrl+Delete", () => Strings.Customize.DeleteQueryLabel),
    ];

    // ---- ナビゲーション ----
    public static KeyChord OpenInBrowser => Resolve("openInBrowser");
    public static KeyChord Back => Resolve("back");
    public static KeyChord Home => Resolve("home");

    /// <summary>「戻る」キーの表示名（項目の副題などのヒント用）。</summary>
    public static string BackLabel => BindingText("back");

    // ---- チケット操作（番号検索・履歴・クエリ結果・コメントページで共通） ----
    public static KeyChord CopyLink => Resolve("copyLink");
    public static KeyChord Refresh => Resolve("refresh");
    public static KeyChord LoadMore => Resolve("loadMore");
    public static KeyChord ToggleOrder => Resolve("toggleOrder");
    public static KeyChord ChangeStatus => Resolve("changeStatus");
    public static KeyChord AddComment => Resolve("addComment");

    // ---- 保存クエリ管理 ----
    public static KeyChord AddQuery => Resolve("addQuery");
    public static KeyChord EditQuery => Resolve("editQuery");
    public static KeyChord DeleteQuery => Resolve("deleteQuery");

    private static UiConfigStore? _config;

    /// <summary>起動時に一度呼び、上書き設定を参照できるようにする。</summary>
    public static void Configure(UiConfigStore config) => _config = config;

    /// <summary>現在の（上書き済みの）バインド文字列。フォームの初期値・ヒント表示用。</summary>
    public static string BindingText(string actionId)
    {
        var overridden = _config?.KeybindingOverride(actionId);
        if (overridden is not null && TryParse(overridden, out _))
        {
            return overridden;
        }

        return DefaultBinding(actionId);
    }

    /// <summary>
    /// "Ctrl+Shift+K" 形式を KeyChord に解析する。修飾キーは短縮形 C / A / S / W も受け付ける
    /// （最後のトークンだけがキー扱いなので、"Ctrl+S" の S はキー、"C+S+K" の S は Shift）。
    /// 誤って通常入力を奪わないよう Ctrl / Alt / Win いずれかを必須とする（Shift 単独は不可）。
    /// </summary>
    public static bool TryParse(string text, out KeyChord chord)
    {
        chord = default;
        var tokens = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        var ctrl = false;
        var alt = false;
        var shift = false;
        var win = false;

        // 最後のトークンがキー、それ以前はすべて修飾キー。
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            switch (tokens[i].ToUpperInvariant())
            {
                case "CTRL" or "CONTROL" or "C": ctrl = true; break;
                case "ALT" or "A": alt = true; break;
                case "SHIFT" or "S": shift = true; break;
                case "WIN" or "WINDOWS" or "W": win = true; break;
                default: return false;
            }
        }

        if (!(ctrl || alt || win) || !TryParseKey(tokens[^1], out var key))
        {
            return false;
        }

        chord = KeyChordHelpers.FromModifiers(ctrl: ctrl, alt: alt, shift: shift, win: win, vkey: key, scanCode: 0);
        return true;
    }

    private static bool TryParseKey(string name, out VirtualKey key)
    {
        // よく使う別名を先に解決し、残りは VirtualKey 名（A〜Z, F1〜F12, Left 等）として解析。
        switch (name.ToUpperInvariant())
        {
            case "ESC": key = VirtualKey.Escape; return true;
            case "DEL": key = VirtualKey.Delete; return true;
            case "RETURN": key = VirtualKey.Enter; return true;
            case "BACKSPACE": key = VirtualKey.Back; return true;
            case "←": key = VirtualKey.Left; return true;
            case "→": key = VirtualKey.Right; return true;
            case "↑": key = VirtualKey.Up; return true;
            case "↓": key = VirtualKey.Down; return true;
        }

        if (name.Length == 1 && name[0] is >= '0' and <= '9')
        {
            key = VirtualKey.Number0 + (name[0] - '0');
            return true;
        }

        return Enum.TryParse(name, ignoreCase: true, out key) && key != 0;
    }

    private static KeyChord Resolve(string actionId)
    {
        var overridden = _config?.KeybindingOverride(actionId);
        if (overridden is not null && TryParse(overridden, out var chord))
        {
            return chord;
        }

        // 既定は Actions の定義から（必ず解析可能。万一失敗したら既定値の KeyChord=無割当）。
        return TryParse(DefaultBinding(actionId), out var fallback) ? fallback : default;
    }

    private static string DefaultBinding(string actionId)
    {
        foreach (var action in Actions)
        {
            if (action.Id == actionId)
            {
                return action.DefaultBinding;
            }
        }

        throw new KeyNotFoundException($"Unknown keybinding action: {actionId}");
    }
}
