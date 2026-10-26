using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HikvisionFourDisplay.Runtime.Security
{
    public readonly struct FeedCredential
    {
        public readonly string Username;
        public readonly string Password;

        public FeedCredential(string username, string password)
        {
            Username = username ?? string.Empty;
            Password = password ?? string.Empty;
        }
    }

    public interface ICredentialProvider
    {
        bool TryGet(string credentialKey, out FeedCredential credential);
    }

    /// <summary>
    /// Stores feed credentials as CRED_TYPE_GENERIC entries in the Windows Credential
    /// Manager, DPAPI-protected under the current Windows user profile. No plaintext
    /// secret ever touches the project, a config file, or a log line.
    /// </summary>
    public sealed class WindowsCredentialManagerProvider : ICredentialProvider
    {
        private const string TargetPrefix = "HikvisionFourDisplay:";
        private const uint CredTypeGeneric = 1;
        private const uint CredPersistLocalMachine = 2;

        public bool TryGet(string credentialKey, out FeedCredential credential)
        {
            credential = default;
            if (string.IsNullOrWhiteSpace(credentialKey)) return false;

            var target = TargetPrefix + credentialKey;
            if (!NativeMethods.CredRead(target, CredTypeGeneric, 0, out var credentialPtr))
                return false;

            try
            {
                var native = Marshal.PtrToStructure<NativeMethods.NativeCredential>(credentialPtr);
                var username = native.UserName ?? string.Empty;
                var password = string.Empty;

                if (native.CredentialBlobSize > 0 && native.CredentialBlob != IntPtr.Zero)
                {
                    var bytes = new byte[native.CredentialBlobSize];
                    Marshal.Copy(native.CredentialBlob, bytes, 0, bytes.Length);
                    password = Encoding.Unicode.GetString(bytes);
                }

                credential = new FeedCredential(username, password);
                return true;
            }
            finally
            {
                NativeMethods.CredFree(credentialPtr);
            }
        }

        public static bool Save(string credentialKey, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(credentialKey)) return false;

            var target = TargetPrefix + credentialKey;
            var passwordBytes = Encoding.Unicode.GetBytes(password ?? string.Empty);
            var blobPtr = Marshal.AllocHGlobal(Math.Max(passwordBytes.Length, 1));

            try
            {
                Marshal.Copy(passwordBytes, 0, blobPtr, passwordBytes.Length);

                var native = new NativeMethods.NativeCredential
                {
                    Flags = 0,
                    Type = CredTypeGeneric,
                    TargetName = target,
                    Comment = "HikvisionFourDisplay feed credential",
                    CredentialBlobSize = (uint)passwordBytes.Length,
                    CredentialBlob = blobPtr,
                    Persist = CredPersistLocalMachine,
                    UserName = username ?? string.Empty
                };

                return NativeMethods.CredWrite(ref native, 0);
            }
            finally
            {
                ZeroAndFree(blobPtr, passwordBytes.Length);
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
            }
        }

        public static bool Delete(string credentialKey)
        {
            if (string.IsNullOrWhiteSpace(credentialKey)) return false;
            return NativeMethods.CredDelete(TargetPrefix + credentialKey, CredTypeGeneric, 0);
        }

        private static void ZeroAndFree(IntPtr ptr, int length)
        {
            if (ptr == IntPtr.Zero) return;
            if (length > 0)
            {
                var zeros = new byte[length];
                Marshal.Copy(zeros, 0, ptr, length);
            }
            Marshal.FreeHGlobal(ptr);
        }

        private static class NativeMethods
        {
            [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPtr);

            [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredWrite(ref NativeCredential credential, uint flags);

            [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern bool CredDelete(string target, uint type, int flags);

            [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
            public static extern void CredFree(IntPtr buffer);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct NativeCredential
            {
                public uint Flags;
                public uint Type;
                public string TargetName;
                public string Comment;
                public long LastWritten;
                public uint CredentialBlobSize;
                public IntPtr CredentialBlob;
                public uint Persist;
                public uint AttributeCount;
                public IntPtr Attributes;
                public string TargetAlias;
                public string UserName;
            }
        }
    }
}
