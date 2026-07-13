using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// MEGA client-side crypto (anonymous folder shares). The exact byte-order + CTR-counter behaviour was
/// validated LIVE against a real folder by <c>devtools/mega-probe.mjs</c> (decrypted real filenames + a
/// read-me's UTF-8 body); these tests lock the C# port's self-consistency + the sensitive word-order logic.
/// </summary>
public class MegaCryptoTests
{
    [Fact]
    public void Base64UrlDecode_HandlesUrlSafe_NoPadding()
    {
        // The real folder key from the validated probe — url-safe base64, no padding → 16 bytes.
        MegaCrypto.Base64UrlDecode("lCWpVl5ZfkhRTsZwskmdIA").Length.Should().Be(16);
    }

    [Fact]
    public void UnpackFileKey_XorsWordPairs_AndTakesNonceFromWords4And5()
    {
        // 8 big-endian u32 words 1..8 → aesKey = [w0^w4, w1^w5, w2^w6, w3^w7]; nonce = [w4, w5].
        var k = new byte[32];
        for (uint i = 0; i < 8; i++) BinaryPrimitives.WriteUInt32BigEndian(k.AsSpan((int)i * 4), i + 1);

        var (aes, nonce) = MegaCrypto.UnpackFileKey(k);

        BinaryPrimitives.ReadUInt32BigEndian(aes.AsSpan(0)).Should().Be(1u ^ 5u);
        BinaryPrimitives.ReadUInt32BigEndian(aes.AsSpan(4)).Should().Be(2u ^ 6u);
        BinaryPrimitives.ReadUInt32BigEndian(aes.AsSpan(8)).Should().Be(3u ^ 7u);
        BinaryPrimitives.ReadUInt32BigEndian(aes.AsSpan(12)).Should().Be(4u ^ 8u);
        BinaryPrimitives.ReadUInt32BigEndian(nonce.AsSpan(0)).Should().Be(5u);
        BinaryPrimitives.ReadUInt32BigEndian(nonce.AsSpan(4)).Should().Be(6u);
    }

    [Fact]
    public void DecryptAttrName_ReadsMegaPrefixedJson_Utf8()
    {
        var key = RandomNumberGenerator.GetBytes(16);
        var plain = Encoding.UTF8.GetBytes("MEGA{\"n\":\"珂蕾妲.ini\"}");
        var padded = new byte[(plain.Length + 15) / 16 * 16];
        Array.Copy(plain, padded, plain.Length); // zero-padded, like MEGA
        using var aes = Aes.Create();
        aes.Key = key;
        var enc = aes.EncryptCbc(padded, new byte[16], PaddingMode.None);
        var b64 = Convert.ToBase64String(enc).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        MegaCrypto.DecryptAttrName(key, b64).Should().Be("珂蕾妲.ini");
    }

    [Fact]
    public void DecryptAttrName_NonMegaPrefix_ReturnsNull()
    {
        var key = RandomNumberGenerator.GetBytes(16);
        using var aes = Aes.Create();
        aes.Key = key;
        var enc = aes.EncryptCbc(new byte[16], new byte[16], PaddingMode.None); // decrypts to zeros, no "MEGA"
        var b64 = Convert.ToBase64String(enc).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        MegaCrypto.DecryptAttrName(key, b64).Should().BeNull();
    }

    [Fact]
    public async Task DecryptCtr_IsSelfInverse_AcrossChunkBoundary()
    {
        // CTR is symmetric (XOR keystream): applying it twice returns the original — exercises the keystream
        // generation, incl. the >64 KB chunk-boundary + non-16-aligned tail. Real MEGA vectors: mega-probe.mjs.
        var aesKey = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(8);
        var plain = RandomNumberGenerator.GetBytes(70_003); // > 64 KB, not 16-aligned

        var enc = new MemoryStream();
        await MegaCrypto.DecryptCtrAsync(new MemoryStream(plain), enc, aesKey, nonce, default);
        enc.ToArray().Should().NotBeEquivalentTo(plain, "the keystream actually transforms the bytes");

        enc.Position = 0;
        var dec = new MemoryStream();
        await MegaCrypto.DecryptCtrAsync(enc, dec, aesKey, nonce, default);
        dec.ToArray().Should().Equal(plain);
    }

    [Fact]
    public void ParseFolderLink_ExtractsIdAndKey()
    {
        var (id, key) = MegaShareResolver.ParseFolderLink("https://mega.nz/folder/P7JhGJaB#lCWpVl5ZfkhRTsZwskmdIA");
        id.Should().Be("P7JhGJaB");
        key.Length.Should().Be(16);
    }

    [Theory]
    [InlineData("https://mega.nz/file/abc#key")]        // file link (unsupported)
    [InlineData("https://mega.nz/folder/abc")]          // no key
    [InlineData("https://pan.quark.cn/s/x")]            // not MEGA
    public void ParseFolderLink_RejectsNonFolderLinks(string url)
    {
        var act = () => MegaShareResolver.ParseFolderLink(url);
        act.Should().Throw<OperationException>().Which.Code.Should().Be("MEGA_LINK_UNSUPPORTED");
    }
}
