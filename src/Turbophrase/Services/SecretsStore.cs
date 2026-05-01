using System.Runtime.InteropServices;
using System.Text;
using Turbophrase.Core.Configuration;

namespace Turbophrase.Services;

/// <summary>
/// Wraps the Windows Credential Manager (advapi32 <c>CredRead</c>/<c>CredWrite</c>/
/// <c>CredDelete</c>) and exposes <see cref="ISecretsResolver"/> so the Core
/// configuration layer can resolve <c>@credman:NAME</c> references without
/// taking a Win32 dependency itself.
/// </summary>
/// <remarks>
/// All credentials are stored under target names of the form
/// <c>Turbophrase:&lt;name&gt;</c>. Persistence uses
/// <see cref="CredPersist.Enterprise"/> so the values roam with the user
/// account when supported. No special MSIX capability is required to call
/// these APIs from a packaged or unpackaged process.
/// </remarks>
public sealed class SecretsStore : ISecretsResolver
{
    private const string TargetPrefix = "Turbophrase:";
    private const int ERROR_NOT_FOUND = 1168;

    /// <summary>
    /// Returns the Credential Manager target name used for the given logical
    /// name. Exposed so callers can present it in the UI.
    /// </summary>
    public static string GetTargetName(string name) => TargetPrefix + name;

    /// <inheritdoc />
    public string? TryRead(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var target = GetTargetName(name);
        if (!CredRead(target, CredType.Generic, 0, out var credPtr))
        {
            return null;
        }

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
            {
                return string.Empty;
            }

            // CredentialBlob is a UTF-16 (Unicode) byte array per Microsoft's
            // recommended convention for string secrets. The size is in bytes.
            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    /// <summary>
    /// Stores a secret under the given logical name. Overwrites any existing
    /// value.
    /// </summary>
    public void Save(string name, string secret)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Secret name cannot be empty.", nameof(name));
        }

        secret ??= string.Empty;

        var target = GetTargetName(name);
        var blob = Encoding.Unicode.GetBytes(secret);
        var blobPtr = IntPtr.Zero;

        try
        {
            blobPtr = Marshal.AllocHGlobal(blob.Length);
            Marshal.Copy(blob, 0, blobPtr, blob.Length);

            var cred = new CREDENTIAL
            {
                Type = (uint)CredType.Generic,
                TargetName = target,
                CredentialBlob = blobPtr,
                CredentialBlobSize = blob.Length,
                Persist = (uint)CredPersist.Enterprise,
                UserName = Environment.UserName,
            };

            if (!CredWrite(ref cred, 0))
            {
                var err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"Failed to save secret '{name}' to Credential Manager (error {err}).");
            }
        }
        finally
        {
            if (blobPtr != IntPtr.Zero)
            {
                // Best-effort: zero the buffer before freeing so the secret
                // does not linger in process memory.
                for (var i = 0; i < blob.Length; i++)
                {
                    Marshal.WriteByte(blobPtr, i, 0);
                }
                Marshal.FreeHGlobal(blobPtr);
            }

            // Wipe the managed copy too. The CLR may still hold the .NET
            // string immutably; this only zeroes the byte[] we built.
            Array.Clear(blob, 0, blob.Length);
        }
    }

    /// <summary>
    /// Removes a secret, returning <c>true</c> if it existed.
    /// </summary>
    public bool Delete(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var target = GetTargetName(name);
        if (CredDelete(target, CredType.Generic, 0))
        {
            return true;
        }

        var err = Marshal.GetLastWin32Error();
        if (err == ERROR_NOT_FOUND)
        {
            return false;
        }

        throw new InvalidOperationException(
            $"Failed to delete secret '{name}' from Credential Manager (error {err}).");
    }

    /// <summary>
    /// Lists all Turbophrase-owned secret names (without the target prefix).
    /// Useful for the Settings UI and the <c>secrets</c> CLI command.
    /// </summary>
    public IReadOnlyList<string> List()
    {
        if (!CredEnumerate($"{TargetPrefix}*", 0, out var count, out var ptr))
        {
            var err = Marshal.GetLastWin32Error();
            if (err == ERROR_NOT_FOUND)
            {
                return Array.Empty<string>();
            }

            return Array.Empty<string>();
        }

        try
        {
            var names = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var entry = Marshal.ReadIntPtr(ptr, i * IntPtr.Size);
                var cred = Marshal.PtrToStructure<CREDENTIAL>(entry);
                if (cred.TargetName != null && cred.TargetName.StartsWith(TargetPrefix, StringComparison.Ordinal))
                {
                    names.Add(cred.TargetName.Substring(TargetPrefix.Length));
                }
            }
            return names;
        }
        finally
        {
            CredFree(ptr);
        }
    }

    // ---------------------------------------------------------------------
    // Win32 interop
    // ---------------------------------------------------------------------

    private enum CredType : uint
    {
        Generic = 1,
    }

    private enum CredPersist : uint
    {
        Session = 1,
        LocalMachine = 2,
        Enterprise = 3,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CredType type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, CredType type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredEnumerate(string? filter, int flags, out int count, out IntPtr credentialsPtr);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);
}
