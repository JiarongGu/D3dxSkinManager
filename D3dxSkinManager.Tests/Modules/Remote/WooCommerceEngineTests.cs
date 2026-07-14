using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;

namespace D3dxSkinManager.Tests.Modules.Remote;

/// <summary>
/// Parsing of the WooCommerce Store API JSON (engine="woocommerce"). Shapes mirror kekehxl.top's live
/// /wp-json/wc/store/v1/products (verified 2026-07-14): product array for lists, [product] for ?slug=,
/// download links as labelled anchors in short_description. See remote-library.md.
/// </summary>
public class WooCommerceEngineTests
{
    // A list page: two products (one full, one image/permalink-only), plus one junk record dropped.
    private const string Products = """
    [
      { "id": 13696, "name": "卡缪-壮硕v1.3", "slug": "kamu", "permalink": "https://kekehxl.top/product/kamu/",
        "images": [ { "src": "https://kekehxl.top/i/a.jpg", "thumbnail": "https://kekehxl.top/i/a-300.jpg" } ],
        "categories": [ { "id": 139, "name": "终末地Mod" } ], "tags": [ { "id": 5, "name": "卡缪" } ] },
      { "id": 2, "name": "玄翎-逆兔", "permalink": "https://kekehxl.top/product/xuanling/", "images": [], "tags": [] },
      { "id": 3, "name": "", "permalink": "https://kekehxl.top/product/empty/" }
    ]
    """;

    // A ?slug= detail: an array with one product whose short_description carries the download anchors.
    private const string ProductDetail = """
    [
      { "id": 13696, "name": "卡缪-壮硕v1.3", "slug": "kamu", "permalink": "https://kekehxl.top/product/kamu/",
        "short_description": "<p><strong>MEGA盘：<a href=\"https://mega.nz/file/AbC#k\">点击获取</a>（需要梯子）</strong></p><p><strong>百度盘：<a href=\"https://pan.baidu.com/s/1abc?pwd=keke\">点击获取</a></strong></p><p><strong>夸克盘：<a href=\"https://pan.quark.cn/s/2ede341a282c\">点击获取</a></strong></p><hr/><p><strong>解压码</strong>：kekehxl</p><p><a href=\"https://vpn-ad.example/register?code=x\">VPN（梯子）</a></p>",
        "description": "",
        "images": [ { "src": "https://kekehxl.top/i/a.jpg" }, { "src": "https://kekehxl.top/i/b.jpg" } ],
        "tags": [ { "id": 5, "name": "卡缪" } ] }
    ]
    """;

    private static List<RemoteResolverRule> Resolvers() => new()
    {
        new() { Match = @"^https?://mega\.nz/(file|folder)/", Type = "mega", Name = "MEGA", UnzipPassword = "kekehxl" },
        new() { Match = @"^https?://pan\.quark\.cn/s/", Type = "quark", Name = "夸克", UnzipPassword = "kekehxl" },
        new() { Match = @"^https?://pan\.baidu\.com/s/", Type = "external", Name = "百度盘" },
    };

    [Fact]
    public void ParseProducts_ExtractsCards_SkipsEmpty_TagsImage_ShortPageIsLast()
    {
        var result = WooCommerceEngine.ParseProducts(Products, page: 1, perPage: 30);

        result.Cards.Should().HaveCount(2); // empty-name product dropped
        var first = result.Cards[0];
        first.Title.Should().Be("卡缪-壮硕v1.3");
        // The numeric product id rides the DetailUrl so detail is fetched by id (slug lookup is unreliable).
        first.DetailUrl.Should().Be("https://kekehxl.top/product/kamu/?wc_id=13696");
        first.ImageUrl.Should().Be("https://kekehxl.top/i/a-300.jpg"); // thumbnail preferred for the card
        first.Tags.Should().ContainSingle().Which.Should().Be("卡缪");
        // 2 records returned < perPage 30 → this is the last page.
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public void ParseProducts_FullPage_LeavesTotalUnknown()
    {
        // perPage 2 with 2 valid+1 junk = 3 records ≥ perPage → not necessarily the last page.
        var result = WooCommerceEngine.ParseProducts(Products, page: 1, perPage: 2);
        result.TotalPages.Should().BeNull();
    }

    [Fact]
    public void ParseProductDetail_MatchesResolvers_DropsAds_CarriesUnzipPassword_Gallery()
    {
        var detail = WooCommerceEngine.ParseProductDetail(ProductDetail, "https://kekehxl.top/product/kamu/", Resolvers());

        detail.Title.Should().Be("卡缪-壮硕v1.3");
        detail.Images.Should().Equal("https://kekehxl.top/i/a.jpg", "https://kekehxl.top/i/b.jpg");
        detail.Tags.Should().ContainSingle().Which.Should().Be("卡缪");

        // MEGA + Baidu + Quark matched in document order; the VPN ad anchor dropped.
        detail.Downloads.Select(d => d.Type).Should().Equal("mega", "external", "quark");
        detail.Downloads.Select(d => d.Name).Should().Equal("MEGA", "百度盘", "夸克");
        detail.Downloads.Should().OnlyContain(d => !d.Url.Contains("vpn-ad"));
        // The archive password rides the importable hosts (used only on a password-failed extract).
        detail.Downloads.Single(d => d.Type == "mega").UnzipPassword.Should().Be("kekehxl");
        detail.Downloads.Single(d => d.Type == "external").UnzipPassword.Should().BeNull();
    }

    [Fact]
    public void ParseProductDetail_EmptyArray_ThrowsNotJson()
    {
        var act = () => WooCommerceEngine.ParseProductDetail("[]", "https://kekehxl.top/product/gone/", Resolvers());
        act.Should().Throw<OperationException>().Which.Code.Should().Be("REMOTE_DETAIL_NOT_JSON");
    }

    [Theory]
    [InlineData("https://kekehxl.top/product/kamu/?wc_id=13696", "13696")]
    [InlineData("https://kekehxl.top/product/kamu/?a=1&wc_id=42", "42")]
    [InlineData("https://kekehxl.top/product/kamu/", null)]
    public void ExtractProductId_ReadsWcId(string detailUrl, string? expected)
    {
        WooCommerceEngine.ExtractProductId(detailUrl).Should().Be(expected);
    }

    [Fact]
    public void UrlBuilders_ShapeTheStoreApiCalls()
    {
        WooCommerceEngine.BuildListUrl("https://kekehxl.top/", "21", 2, 30)
            .Should().Be("https://kekehxl.top/wp-json/wc/store/v1/products?per_page=30&page=2&orderby=date&order=desc&category=21");
        WooCommerceEngine.BuildSearchUrl("https://kekehxl.top", "卡缪", "21", 30)
            .Should().Be("https://kekehxl.top/wp-json/wc/store/v1/products?per_page=30&search=%E5%8D%A1%E7%BC%AA&category=21");
        WooCommerceEngine.BuildProductUrl("https://kekehxl.top", "13696")
            .Should().Be("https://kekehxl.top/wp-json/wc/store/v1/products/13696");
    }
}
