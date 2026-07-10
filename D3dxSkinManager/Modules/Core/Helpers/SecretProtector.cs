using System.Security.Cryptography;
using Encoding = System.Text.Encoding;

namespace D3dxSkinManager.Modules.Core.Helpers;

/// <summary>
/// At-rest protection for stored secrets (session cookies/tokens) via Windows DPAPI,
/// <see cref="DataProtectionScope.CurrentUser"/> — the ciphertext only decrypts for the SAME
/// Windows user on the SAME machine. A copied/tampered/foreign blob throws on Unprotect; callers
/// treat that as "token doesn't match → invalidate" (drop the secret, require re-login).
/// </summary>
public static class SecretProtector
{
    // App-scoped entropy: ties the blob to this app on top of the user/machine binding.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("D3dxSkinManager.secret.v1");

    /// <summary>Encrypt a secret for the current Windows user; returns base64 ciphertext.</summary>
    public static string Protect(string secret) =>
        Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser));

    /// <summary>
    /// Decrypt a base64 ciphertext produced by <see cref="Protect"/>. Throws
    /// <see cref="CryptographicException"/> (or <see cref="FormatException"/> for corrupt base64)
    /// when the blob was made by another user/machine or was tampered with — the caller's
    /// invalidate signal.
    /// </summary>
    public static string Unprotect(string protectedBase64) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(
            Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser));
}
