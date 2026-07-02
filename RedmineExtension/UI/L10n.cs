using System.Globalization;

namespace RedmineExtension;

/// <summary>
/// 最小限のローカライズ。Windows の表示言語が日本語なら日本語、それ以外は英語を返す。
/// resw/PRI を使わないのは、AOT/トリミング安全を保ちつつ、訳文がコードの利用箇所の隣に
/// 並んで差分レビューしやすいようにするため。UI 文字列は必ず T(ja, en) で書くこと。
/// </summary>
internal static class L10n
{
    /// <summary>表示言語が日本語かどうか（プロセス起動時に確定）。</summary>
    public static readonly bool IsJapanese =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";

    /// <summary>日本語 UI なら ja、それ以外は en を返す。</summary>
    public static string T(string ja, string en) => IsJapanese ? ja : en;
}
