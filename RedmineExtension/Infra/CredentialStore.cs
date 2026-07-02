using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RedmineExtension;

/// <summary>
/// Windows 資格情報マネージャ(汎用資格情報)に文字列シークレットを保存/取得/削除する。
/// DPAPI によりユーザー単位で暗号化され、コントロールパネルの資格情報マネージャに表示される。
/// </summary>
internal static partial class CredentialStore
{
    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    // x64 の CREDENTIALW フィールドオフセット(読み取りに必要な分のみ)。
    private const int OffsetCredentialBlobSize = 32;
    private const int OffsetCredentialBlob = 40;

    public static bool Save(string target, string secret)
    {
        var blob = Encoding.Unicode.GetBytes(secret);
        var targetPtr = Marshal.StringToHGlobalUni(target);
        var userPtr = Marshal.StringToHGlobalUni(target);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);

            var cred = new CREDENTIALW
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = targetPtr,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = userPtr,
            };

            return CredWriteW(in cred, 0);
        }
        finally
        {
            // Windows が値をコピーするため、書き込み後は確保したメモリを解放してよい。
            Marshal.FreeHGlobal(targetPtr);
            Marshal.FreeHGlobal(userPtr);
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public static string? Read(string target)
    {
        if (!CredReadW(target, CRED_TYPE_GENERIC, 0, out var credPtr) || credPtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var size = Marshal.ReadInt32(credPtr, OffsetCredentialBlobSize);
            var blob = Marshal.ReadIntPtr(credPtr, OffsetCredentialBlob);
            if (size <= 0 || blob == IntPtr.Zero)
            {
                return string.Empty;
            }

            var buffer = new byte[size];
            Marshal.Copy(blob, buffer, 0, size);
            return Encoding.Unicode.GetString(buffer);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public static void Delete(string target) => CredDeleteW(target, CRED_TYPE_GENERIC, 0);

    [StructLayout(LayoutKind.Sequential)]
    private struct CREDENTIALW
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWriteW(in CREDENTIALW credential, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDeleteW(string target, uint type, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
    private static partial void CredFree(IntPtr buffer);
}
