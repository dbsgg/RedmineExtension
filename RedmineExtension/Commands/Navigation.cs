using Microsoft.CommandPalette.Extensions.Toolkit;

namespace RedmineExtension;

/// <summary>
/// ページ階層を戻る導線。パレット設定によっては Esc が「閉じる」に割当てられていて
/// ページ遷移に使えないため、明示的な「戻る」を全一覧ページの項目に用意する。
/// キーは <see cref="Keybindings.Back"/>（設定で変更可能）。
/// </summary>
internal static class Navigation
{
    private static readonly IconInfo HomeIcon = new IconInfo(""); // glyph:E80F

    private static readonly IconInfo BackIcon = new IconInfo(""); // glyph:E72B

    /// <summary>コンテキストの「戻る」（既定 Alt+←。ブラウザ/エクスプローラーと同じ慣例）。</summary>
    public static CommandContextItem BackContext() =>
        new(new AnonymousCommand(static () => { })
        {
            Name = Strings.Common.Back,
            Icon = BackIcon,
            Result = CommandResult.GoBack(),
        })
        {
            RequestedShortcut = Keybindings.Back,
        };

    /// <summary>Alt+Home（既定）= 階層を一気に抜けてパレットのホームへ戻る。</summary>
    public static CommandContextItem HomeContext() =>
        new(new AnonymousCommand(static () => { })
        {
            Name = Strings.Common.Home,
            Icon = HomeIcon,
            Result = CommandResult.GoHome(),
        })
        {
            RequestedShortcut = Keybindings.Home,
        };

    /// <summary>一覧の末尾に置く可視の「戻る」項目（ショートカットを知らなくても戻れるように）。
    /// コンテキストに「ホームへ戻る」も持ち、深い階層から一気に抜けられる。</summary>
    public static ListItem BackItem() =>
        new(new AnonymousCommand(static () => { })
        {
            Name = Strings.Common.Back,
            Result = CommandResult.GoBack(),
        })
        {
            Title = Strings.Common.BackItemTitle,
            Subtitle = Strings.Common.BackItemSubtitle(Keybindings.BackLabel),
            Icon = BackIcon,
            MoreCommands = [HomeContext()],
        };
}
