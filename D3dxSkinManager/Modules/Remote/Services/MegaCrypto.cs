using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace D3dxSkinManager.Modules.Remote.Services;

/// <summary>
/// MEGA (mega.nz) client-side crypto for ANONYMOUS folder/file shares. VALIDATED end-to-end against a real
/// folder by <c>devtools/mega-probe.mjs</c> (decrypted real filenames + a read-me's UTF-8 body): base64url
/// keys, AES-ECB node-key unwrap, big-endian u32 file-key unpack, AES-CBC "MEGA"-prefixed attributes, and
/// AES-CTR file decrypt. All pure/static — unit-tested by round-trip in <c>MegaCryptoTests</c>.
/// </summary>
public static class MegaCrypto
{
    /// <summary>MEGA uses URL-safe base64 WITHOUT padding.</summary>
    public static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch { 2 => s + "==", 3 => s + "=", _ => s };
        return Convert.FromBase64String(s);
    }

    /// <summary>AES-128-ECB decrypt (no padding) — unwraps a node's key with the shared folder key.</summary>
    public static byte[] DecryptEcb(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return aes.DecryptEcb(data, PaddingMode.None);
    }

    /// <summary>Unpack a FILE node's 32-byte key (8 big-endian u32 words) into the AES key + CTR nonce:
    /// aesKey = [w0^w4, w1^w5, w2^w6, w3^w7]; nonce = [w4, w5] (w6/w7 are the MAC — unused here).</summary>
    public static (byte[] AesKey, byte[] Nonce) UnpackFileKey(byte[] k)
    {
        Span<uint> w = stackalloc uint[8];
        for (var i = 0; i < 8; i++) w[i] = BinaryPrimitives.ReadUInt32BigEndian(k.AsSpan(i * 4, 4));
        var aesKey = new byte[16];
        BinaryPrimitives.WriteUInt32BigEndian(aesKey.AsSpan(0), w[0] ^ w[4]);
        BinaryPrimitives.WriteUInt32BigEndian(aesKey.AsSpan(4), w[1] ^ w[5]);
        BinaryPrimitives.WriteUInt32BigEndian(aesKey.AsSpan(8), w[2] ^ w[6]);
        BinaryPrimitives.WriteUInt32BigEndian(aesKey.AsSpan(12), w[3] ^ w[7]);
        var nonce = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(0), w[4]);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(4), w[5]);
        return (aesKey, nonce);
    }

    /// <summary>Decrypt a node's attribute blob (AES-CBC, zero IV, no padding) → its NAME (<c>n</c>). MEGA
    /// prefixes the plaintext with "MEGA" then JSON, zero-padded; returns null if it isn't that shape.</summary>
    public static string? DecryptAttrName(byte[] aesKey, string? attrB64)
    {
        if (string.IsNullOrEmpty(attrB64)) return null;
        var buf = Base64UrlDecode(attrB64);
        var len = buf.Length - (buf.Length % 16);
        if (len <= 0) return null;
        using var aes = Aes.Create();
        aes.Key = aesKey;
        var plain = aes.DecryptCbc(buf.AsSpan(0, len), new byte[16], PaddingMode.None);
        var text = Encoding.UTF8.GetString(plain).TrimEnd('\0');
        if (!text.StartsWith("MEGA", StringComparison.Ordinal)) return null;
        try { return JsonDocument.Parse(text[4..]).RootElement.GetProperty("n").GetString(); }
        catch { return null; }
    }

    /// <summary>AES-CTR decrypt a stream (IV = nonce(8) ‖ big-endian block index(8), counting from 0).
    /// CTR is a stream cipher, so a non-16-aligned final block XORs only the bytes present.</summary>
    public static async Task DecryptCtrAsync(Stream input, Stream output, byte[] aesKey, byte[] nonce, CancellationToken ct)
    {
        using var aes = Aes.Create();
        aes.Key = aesKey;
        const int chunkBlocks = 4096;                 // 64 KB per ECB keystream batch
        var buffer = new byte[chunkBlocks * 16];
        var counters = new byte[chunkBlocks * 16];
        ulong block = 0;
        int read;
        while ((read = await ReadUpToAsync(input, buffer, ct).ConfigureAwait(false)) > 0)
        {
            var blocks = (read + 15) / 16;
            for (var b = 0; b < blocks; b++)
            {
                Array.Copy(nonce, 0, counters, b * 16, 8);
                BinaryPrimitives.WriteUInt64BigEndian(counters.AsSpan(b * 16 + 8, 8), block + (ulong)b);
            }
            var keystream = aes.EncryptEcb(counters.AsSpan(0, blocks * 16), PaddingMode.None);
            for (var i = 0; i < read; i++) buffer[i] ^= keystream[i];
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            block += (ulong)blocks;
        }
    }

    private static async Task<int> ReadUpToAsync(Stream s, byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await s.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false);
            if (n == 0) break;
            total += n;
        }
        return total;
    }
}
