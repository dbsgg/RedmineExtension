using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace RedmineExtension;

/// <summary>
/// Win32 OLE クリップボードに「HTML Format」(CF_HTML) とプレーンテキスト(CF_UNICODETEXT)を
/// 同時に書き込むヘルパー。CmdPal 拡張は MTA な COM サーバーとして動くため WinRT の
/// Clipboard ではなく従来のクリップボード API を使う(トーキットの ClipboardHelper と同方針)。
/// HTML Format を入れることで Teams / Outlook / Word 等にクリック可能なリンクとして貼れる。
/// </summary>
internal static partial class RichLinkClipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    /// <summary>表示テキスト <paramref name="label"/>、リンク先 <paramref name="url"/> のハイパーリンクをコピーする。</summary>
    public static void SetHyperlink(string label, string url)
    {
        var fragment = $"<a href=\"{WebUtility.HtmlEncode(url)}\">{WebUtility.HtmlEncode(label)}</a>";
        var cfHtml = BuildCfHtml(fragment);
        // リンク非対応の貼り付け先では「#番号 タイトル」だけにする(URL は HTML の href 側のみ)。
        var plain = label;

        if (!OpenClipboard(IntPtr.Zero))
        {
            throw new InvalidOperationException(Strings.Tickets.ClipboardOpenFailed);
        }

        try
        {
            EmptyClipboard();
            WriteGlobal(CF_UNICODETEXT, Encoding.Unicode.GetBytes(plain + '\0'));

            var htmlFormat = RegisterClipboardFormat("HTML Format");
            if (htmlFormat != 0)
            {
                // CF_HTML は UTF-8 で格納する。
                WriteGlobal(htmlFormat, Encoding.UTF8.GetBytes(cfHtml + '\0'));
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void WriteGlobal(uint format, byte[] bytes)
    {
        var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (hMem == IntPtr.Zero)
        {
            throw new InvalidOperationException(Strings.Tickets.ClipboardAllocFailed);
        }

        var ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero)
        {
            GlobalFree(hMem);
            throw new InvalidOperationException(Strings.Tickets.ClipboardAllocFailed);
        }

        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
        }
        finally
        {
            GlobalUnlock(hMem);
        }

        // 成功するとメモリの所有権がクリップボードに移るため解放しない。失敗時のみ解放。
        if (SetClipboardData(format, hMem) == IntPtr.Zero)
        {
            GlobalFree(hMem);
            throw new InvalidOperationException(Strings.Tickets.ClipboardWriteFailed);
        }
    }

    /// <summary>CF_HTML 形式の文字列を組み立てる。オフセットは UTF-8 バイト基準。</summary>
    private static string BuildCfHtml(string fragment)
    {
        const string open = "<html><body><!--StartFragment-->";
        const string close = "<!--EndFragment--></body></html>";

        // オフセットは 8 桁ゼロ埋めするためヘッダのバイト長は値に依らず一定。
        static string Header(int startHtml, int endHtml, int startFrag, int endFrag) =>
            "Version:0.9\r\n" +
            $"StartHTML:{startHtml:00000000}\r\n" +
            $"EndHTML:{endHtml:00000000}\r\n" +
            $"StartFragment:{startFrag:00000000}\r\n" +
            $"EndFragment:{endFrag:00000000}\r\n";

        var headerLen = Encoding.UTF8.GetByteCount(Header(0, 0, 0, 0));
        var openLen = Encoding.UTF8.GetByteCount(open);
        var fragLen = Encoding.UTF8.GetByteCount(fragment);
        var closeLen = Encoding.UTF8.GetByteCount(close);

        var startHtml = headerLen;
        var startFrag = headerLen + openLen;
        var endFrag = startFrag + fragLen;
        var endHtml = endFrag + closeLen;

        return Header(startHtml, endHtml, startFrag, endFrag) + open + fragment + close;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr hWndNewOwner);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormat(string lpszFormat);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalLock(IntPtr hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr hMem);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GlobalFree(IntPtr hMem);
}
