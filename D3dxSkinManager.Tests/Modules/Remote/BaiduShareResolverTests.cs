using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Pure parsing for the Baidu share resolver — the surl (base62 after "/s/1") + the ?pwd= extract code.
/// The verify/list/transfer/download flow is authed (needs a live BDUSS) and validated live, not here.
/// </summary>
public class BaiduShareResolverTests
{
    [Theory]
    [InlineData("https://pan.baidu.com/s/1xmj5hE9oECTfls2zHJWn7A?pwd=keke", "xmj5hE9oECTfls2zHJWn7A", "keke")]
    [InlineData("https://pan.baidu.com/s/1abcDEF", "abcDEF", "")]                 // no pwd
    [InlineData("https://pan.baidu.com/s/abcDEF?pwd=1234", "abcDEF", "1234")]     // no leading "1"
    [InlineData("https://pan.baidu.com/s/1abc?x=1&pwd=zz9y", "abc", "zz9y")]      // pwd not first query param
    public void ParseShareUrl_ExtractsSurlAndPwd(string url, string surl, string pwd)
    {
        var result = BaiduShareResolver.ParseShareUrl(url);
        result.Surl.Should().Be(surl);
        result.Pwd.Should().Be(pwd);
    }

    [Theory]
    [InlineData("https://pan.baidu.com/disk/home")]
    [InlineData("not a url")]
    public void ParseShareUrl_RejectsNonShareUrls(string url)
    {
        var act = () => BaiduShareResolver.ParseShareUrl(url);
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_RESOLVE_FAILED");
    }
}
