using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Category.Models;
using D3dxSkinManager.Modules.Category.Services;
using D3dxSkinManager.Modules.Core.Exceptions;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;
using D3dxSkinManager.Modules.Remote.Models;
using D3dxSkinManager.Modules.Remote.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using ProfileModel = D3dxSkinManager.Modules.Profiles.Models.Profile;

namespace D3dxSkinManager.Tests.Modules.Profile.Services;

/// <summary>
/// Locks the ProfileBundleService export→analyze→import round-trip: config sanitization (no machine
/// path leaks in the shared .zip), category tree preservation, remote add-missing-only, and the
/// path-traversal guard on zip extraction. Profile metadata/config/thumbnail go through a mocked global
/// IProfileService; category + remote scoped services are resolved through a mocked IProfileServiceProvider.
/// Uses a real temp filesystem (isolated + cleaned in Dispose — test-only, per use-project-paths.md).
/// </summary>
public class ProfileBundleServiceTests : IDisposable
{
    private readonly string _root;
    private readonly Mock<IProfileService> _profileService = new();
    private readonly Mock<IGlobalPathService> _globalPaths = new();
    private readonly Mock<IPathHelper> _pathHelper = new();
    private readonly Mock<IProfileServiceProvider> _profileServices = new();
    private readonly Mock<IServiceProvider> _scope = new();
    private readonly Mock<IProcessRegistry> _process = new();
    private readonly ProfileBundleService _service;

    // Scoped services (resolved via the router mock)
    private readonly Mock<ICategoryService> _categoryService = new();
    private readonly Mock<IRemoteLibraryStore> _libraryStore = new();
    private readonly Mock<IRemoteSourceStore> _sourceStore = new();
    private readonly Mock<IRemoteTagLabelStore> _tagLabelStore = new();

    public ProfileBundleServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "d3dx-bundle-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        _globalPaths.Setup(p => p.GetProfileDirectoryPath(It.IsAny<string>()))
            .Returns<string>(id => Path.Combine(_root, "profiles", id));
        _globalPaths.Setup(p => p.GetProfileThumbnailsDirectory(It.IsAny<string>()))
            .Returns<string>(id => Path.Combine(_root, "profiles", id, "thumbnails"));
        _process.Setup(p => p.Start(It.IsAny<D3dxSkinManager.Modules.Core.Models.ProcessType>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns("proc-1");

        _profileServices.Setup(p => p.GetProfileServices(It.IsAny<string>())).Returns(_scope.Object);
        _scope.Setup(s => s.GetService(typeof(ICategoryService))).Returns(_categoryService.Object);
        _scope.Setup(s => s.GetService(typeof(IRemoteLibraryStore))).Returns(_libraryStore.Object);
        _scope.Setup(s => s.GetService(typeof(IRemoteSourceStore))).Returns(_sourceStore.Object);
        _scope.Setup(s => s.GetService(typeof(IRemoteTagLabelStore))).Returns(_tagLabelStore.Object);

        _service = new ProfileBundleService(
            _profileService.Object, _globalPaths.Object, _pathHelper.Object,
            _profileServices.Object, new PathValidator(), _process.Object,
            new Mock<ILogHelper>().Object);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    // ===== helpers =====

    private string OutputFolder() { var d = Path.Combine(_root, "out"); Directory.CreateDirectory(d); return d; }

    private void ArrangeSourceProfile(string id, ProfileModel profile, ProfileConfiguration config)
    {
        _profileService.Setup(s => s.GetProfileByIdAsync(id)).ReturnsAsync(profile);
        _profileService.Setup(s => s.GetProfileConfigurationAsync(id)).ReturnsAsync(config);
    }

    /// <summary>Mock CreateProfileAsync to return a new profile and capture the request.</summary>
    private (Func<CreateProfileRequest?> Request, string NewId) ArrangeCreateProfile(string newId)
    {
        CreateProfileRequest? captured = null;
        _profileService.Setup(s => s.CreateProfileAsync(It.IsAny<CreateProfileRequest>()))
            .Callback<CreateProfileRequest>(r => captured = r)
            .ReturnsAsync((CreateProfileRequest r) => new ProfileModel { Id = newId, Name = r.Name, Description = r.Description, Color = r.Color, GameName = r.GameName });
        _profileService.Setup(s => s.UpdateProfileConfigurationAsync(It.IsAny<ProfileConfiguration>())).ReturnsAsync(true);
        _profileService.Setup(s => s.UpdateProfileAsync(It.IsAny<UpdateProfileRequest>())).ReturnsAsync(true);
        return (() => captured, newId);
    }

    private static string ReadManifestJson(string zipPath)
    {
        using var za = ZipFile.OpenRead(zipPath);
        using var s = za.GetEntry("profile.json")!.Open();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    // ===== metadata + config + thumbnail round-trip =====

    [Fact]
    public async Task ExportThenImport_RoundTrips_Metadata_SanitizedConfig_AndThumbnail()
    {
        // A source profile with a real thumbnail file + a config full of machine-specific paths.
        var thumbFile = Path.Combine(_root, "srcthumb.png");
        await File.WriteAllBytesAsync(thumbFile, new byte[] { 1, 2, 3, 4 });
        var source = new ProfileModel { Id = "src", Name = "My Setup", Description = "desc", Color = "#abcdef", GameName = "GI", Thumbnail = thumbFile };
        var config = new ProfileConfiguration
        {
            ProfileId = "src",
            ModWork = new ModWorkConfiguration { Mode = "external", Directory = "D:/mods", CleanupMaxCaches = 7 },
            Launch = new LaunchConfiguration { Path = "C:/games/game.exe", Args = "--dx11" },
            FixTools = new ModFixConfiguration { PythonPath = "C:/py/python.exe", TimeoutMinutes = 9 },
            ModImport = new ModImportConfiguration { CompressionType = "zip", CompressionMode = "ultra" },
        };
        ArrangeSourceProfile("src", source, config);

        var export = await _service.ExportAsync(new ProfileBundleExportConfig
        {
            ProfileId = "src", OutputPath = OutputFolder(), IncludeCategories = false, IncludeRemote = false,
        });

        export.Success.Should().BeTrue();
        File.Exists(export.OutputPath).Should().BeTrue();
        export.OutputPath.Should().EndWith("My Setup.zip");

        // The shareable bundle must NOT leak machine-specific paths (sensitive-info.md).
        var manifestJson = ReadManifestJson(export.OutputPath);
        manifestJson.Should().NotContain("C:/games");
        manifestJson.Should().NotContain("python.exe");
        manifestJson.Should().NotContain("D:/mods");

        var analysis = await _service.AnalyzeAsync(export.OutputPath);
        analysis.IsValid.Should().BeTrue();
        analysis.ProfileName.Should().Be("My Setup");
        analysis.HasThumbnail.Should().BeTrue();
        analysis.CategoryCount.Should().Be(0);

        var (request, newId) = ArrangeCreateProfile("new-id");
        ProfileConfiguration? importedCfg = null;
        _profileService.Setup(s => s.UpdateProfileConfigurationAsync(It.IsAny<ProfileConfiguration>()))
            .Callback<ProfileConfiguration>(c => importedCfg = c).ReturnsAsync(true);
        UpdateProfileRequest? thumbReq = null;
        var thumbExistedAtCall = false; // the real UpdateProfileAsync copies the file out before staging is cleaned
        _profileService.Setup(s => s.UpdateProfileAsync(It.IsAny<UpdateProfileRequest>()))
            .Callback<UpdateProfileRequest>(r => { thumbReq = r; thumbExistedAtCall = File.Exists(r.ThumbnailPath); })
            .ReturnsAsync(true);

        var import = await _service.ImportAsync(new ProfileBundleImportConfig
        {
            BundlePath = export.OutputPath, ImportCategories = false, ImportRemote = false,
        });

        import.Success.Should().BeTrue();
        import.NewProfileId.Should().Be(newId);
        request()!.Name.Should().Be("My Setup");
        request()!.Color.Should().Be("#abcdef");
        request()!.GameName.Should().Be("GI");

        // Config imported but sanitized (machine paths stripped, portable fields kept).
        importedCfg.Should().NotBeNull();
        importedCfg!.ProfileId.Should().Be("new-id");
        importedCfg.ModWork.Mode.Should().Be("internal");
        importedCfg.ModWork.Directory.Should().BeNull();
        importedCfg.ModWork.CleanupMaxCaches.Should().Be(7);       // portable field preserved
        importedCfg.Launch.Path.Should().BeEmpty();
        importedCfg.FixTools.PythonPath.Should().BeEmpty();
        importedCfg.FixTools.TimeoutMinutes.Should().Be(9);        // portable field preserved
        importedCfg.ModImport.CompressionType.Should().Be("zip");  // portable field preserved

        // Thumbnail applied from the extracted bundle file (present when handed to the profile service,
        // then staging is cleaned up).
        thumbReq.Should().NotBeNull();
        thumbReq!.ThumbnailPath.Should().NotBeNullOrEmpty();
        thumbExistedAtCall.Should().BeTrue();
    }

    [Fact]
    public async Task Analyze_FolderWithoutManifest_ReturnsInvalid()
    {
        var empty = Path.Combine(_root, "empty"); Directory.CreateDirectory(empty);
        var analysis = await _service.AnalyzeAsync(empty);
        analysis.IsValid.Should().BeFalse();
        analysis.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Import_ZipWithPathTraversalEntry_IsRejected()
    {
        var manifest = "{\"version\":\"1.0\",\"profileName\":\"Evil\"}";
        var zipPath = Path.Combine(_root, "evil.zip");
        using (var fs = File.Create(zipPath))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(archive.CreateEntry("profile.json").Open())) w.Write(manifest);
            using (var w = new StreamWriter(archive.CreateEntry("../escape.txt").Open())) w.Write("pwned");
        }
        ArrangeCreateProfile("new-id");

        var act = async () => await _service.ImportAsync(new ProfileBundleImportConfig { BundlePath = zipPath });

        var ex = await act.Should().ThrowAsync<OperationException>();
        ex.Which.Code.Should().Be("PROFILE_BUNDLE_UNSAFE_ENTRY");
        File.Exists(Path.Combine(_root, "escape.txt")).Should().BeFalse("the traversal entry must never be written outside the staging dir");
    }

    [Fact]
    public async Task Import_UnsupportedVersion_Throws()
    {
        var folder = Path.Combine(_root, "v2"); Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "profile.json"), "{\"version\":\"9.9\",\"profileName\":\"X\"}");
        ArrangeCreateProfile("new-id");

        var act = async () => await _service.ImportAsync(new ProfileBundleImportConfig { BundlePath = folder });

        (await act.Should().ThrowAsync<OperationException>()).Which.Code.Should().Be("PROFILE_BUNDLE_VERSION_UNSUPPORTED");
    }

    // ===== category tree round-trip =====

    [Fact]
    public async Task ExportThenImport_RoundTrips_CategoryTree_ParentsBeforeChildren_PreservingIds()
    {
        var source = new ProfileModel { Id = "src", Name = "Cats" };
        ArrangeSourceProfile("src", source, new ProfileConfiguration { ProfileId = "src" });

        // Tree: root "characters" → child "keqing"
        var tree = new List<CategoryInfo>
        {
            new()
            {
                Id = "root", Name = "Characters", Priority = 10,
                Children = new List<CategoryInfo> { new() { Id = "child", Name = "Keqing", ParentId = "root", Priority = 5 } },
            },
        };
        _categoryService.Setup(c => c.GetCategoryTreeAsync()).ReturnsAsync(tree);

        var export = await _service.ExportAsync(new ProfileBundleExportConfig
        {
            ProfileId = "src", OutputPath = OutputFolder(), IncludeCategories = true, IncludeRemote = false,
        });
        export.CategoryCount.Should().Be(2);

        // Import: capture the order of created categories to assert parents come first.
        var (_, newId) = ArrangeCreateProfile("new-id");
        var createdOrder = new List<string>();
        _categoryService.Setup(c => c.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _categoryService.Setup(c => c.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Callback<string, string, string?, int, string?, string?>((id, name, parent, prio, desc, thumb) => createdOrder.Add(id))
            .ReturnsAsync((string id, string name, string? parent, int prio, string? desc, string? thumb) =>
                new CategoryInfo { Id = id, Name = name, ParentId = parent });

        var import = await _service.ImportAsync(new ProfileBundleImportConfig
        {
            BundlePath = export.OutputPath, ImportCategories = true, ImportRemote = false,
        });

        import.ImportedCategoryCount.Should().Be(2);
        createdOrder.Should().Equal("root", "child"); // parent before child, ids preserved
        _categoryService.Verify(c => c.InvalidateTreeCache(), Times.Once);
    }

    // ===== remote round-trip (libraries + add-missing-only overlay) =====

    [Fact]
    public async Task ExportThenImport_Remote_LibrariesAndTagLabels_OverlayAddMissingOnly()
    {
        var source = new ProfileModel { Id = "src", Name = "Remote" };
        ArrangeSourceProfile("src", source, new ProfileConfiguration { ProfileId = "src" });

        var library = new RemoteLibrary
        {
            Id = "lib1", SourceId = "huihui", ListId = "gi", Name = "HuiHui GI",
            TagRules = new List<RemoteTagRule> { new() { Name = "Chars", Tags = new List<string> { "character" }, CategoryId = "cat1" } },
            ParamValues = new Dictionary<string, string> { { "region", "cn" } },
        };
        _libraryStore.Setup(s => s.GetState()).Returns(new RemoteLibrariesState { Libraries = new List<RemoteLibrary> { library } });
        var sourceConfig = new RemoteSourceConfig { Id = "huihui", TagLabels = new Dictionary<string, Dictionary<string, string>> { ["en"] = new() { ["character"] = "Character" } } };
        _sourceStore.Setup(s => s.GetById("huihui")).Returns(sourceConfig);
        _tagLabelStore.Setup(s => s.GetForSource("huihui", It.IsAny<Dictionary<string, Dictionary<string, string>>>()))
            .Returns(new Dictionary<string, Dictionary<string, string>> { ["en"] = new() { ["character"] = "Character" } });

        // Export side: source is a customized overlay → included in the bundle.
        _sourceStore.Setup(s => s.GetOrigins()).Returns(new Dictionary<string, string> { ["huihui"] = "customized" });

        var export = await _service.ExportAsync(new ProfileBundleExportConfig
        {
            ProfileId = "src", OutputPath = OutputFolder(), IncludeCategories = false, IncludeRemote = true,
        });
        export.LibraryCount.Should().Be(1);
        var manifestJson = ReadManifestJson(export.OutputPath);
        manifestJson.Should().Contain("huihui");

        // Import side: on the target machine huihui is a shipped default (NOT customized) → overlay added.
        var (_, newId) = ArrangeCreateProfile("new-id");
        _sourceStore.Setup(s => s.GetOrigins()).Returns(new Dictionary<string, string> { ["huihui"] = "default" });
        RemoteSourceConfig? savedOverlay = null;
        _sourceStore.Setup(s => s.Save(It.IsAny<RemoteSourceConfig>()))
            .Callback<RemoteSourceConfig>(c => savedOverlay = c).Returns((RemoteSourceConfig c) => c);
        RemoteLibrary? addedLib = null;
        _libraryStore.Setup(s => s.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<RemoteTagRule>>(), It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, string, List<RemoteTagRule>?, Dictionary<string, string>?>((src, list, name, rules, pv) =>
                addedLib = new RemoteLibrary { SourceId = src, ListId = list, Name = name, TagRules = rules ?? new(), ParamValues = pv ?? new() })
            .Returns(new RemoteLibrary());

        var import = await _service.ImportAsync(new ProfileBundleImportConfig
        {
            BundlePath = export.OutputPath, ImportCategories = false, ImportRemote = true,
        });

        import.ImportedLibraryCount.Should().Be(1);
        import.ImportedSourceOverlayCount.Should().Be(1);
        import.ImportedTagLabelCount.Should().Be(1);
        savedOverlay!.Id.Should().Be("huihui");
        addedLib!.SourceId.Should().Be("huihui");
        addedLib.ListId.Should().Be("gi");
        addedLib.TagRules.Should().ContainSingle(r => r.CategoryId == "cat1");
        _tagLabelStore.Verify(s => s.SetLangLabels("huihui", "en", It.IsAny<Dictionary<string, string>>(),
            It.IsAny<Dictionary<string, Dictionary<string, string>>>()), Times.Once);
    }

    [Fact]
    public async Task Import_Remote_DoesNotOverwriteExistingLocalOverlay()
    {
        var source = new ProfileModel { Id = "src", Name = "Remote" };
        ArrangeSourceProfile("src", source, new ProfileConfiguration { ProfileId = "src" });
        var library = new RemoteLibrary { Id = "lib1", SourceId = "huihui", ListId = "gi", Name = "HuiHui GI" };
        _libraryStore.Setup(s => s.GetState()).Returns(new RemoteLibrariesState { Libraries = new List<RemoteLibrary> { library } });
        _sourceStore.Setup(s => s.GetById("huihui")).Returns(new RemoteSourceConfig { Id = "huihui" });
        _tagLabelStore.Setup(s => s.GetForSource("huihui", It.IsAny<Dictionary<string, Dictionary<string, string>>>()))
            .Returns(new Dictionary<string, Dictionary<string, string>>());
        _sourceStore.Setup(s => s.GetOrigins()).Returns(new Dictionary<string, string> { ["huihui"] = "customized" });

        var export = await _service.ExportAsync(new ProfileBundleExportConfig
        {
            ProfileId = "src", OutputPath = OutputFolder(), IncludeCategories = false, IncludeRemote = true,
        });

        ArrangeCreateProfile("new-id");
        // Target ALREADY has a local customization for huihui → import must NOT overwrite it.
        _sourceStore.Setup(s => s.GetOrigins()).Returns(new Dictionary<string, string> { ["huihui"] = "customized" });
        _libraryStore.Setup(s => s.Add(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<List<RemoteTagRule>>(), It.IsAny<Dictionary<string, string>>())).Returns(new RemoteLibrary());

        var import = await _service.ImportAsync(new ProfileBundleImportConfig
        {
            BundlePath = export.OutputPath, ImportCategories = false, ImportRemote = true,
        });

        import.ImportedSourceOverlayCount.Should().Be(0);
        _sourceStore.Verify(s => s.Save(It.IsAny<RemoteSourceConfig>()), Times.Never);
    }
}
