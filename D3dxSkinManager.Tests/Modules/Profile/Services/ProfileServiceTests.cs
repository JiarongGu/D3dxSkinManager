using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Core.Helpers;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Profiles.Models;
using D3dxSkinManager.Modules.Profiles.Services;

namespace D3dxSkinManager.Tests.Modules.Profile.Services;

/// <summary>
/// Tests for ProfileService CRUD lifecycle + active-profile handling. Repository + helpers are mocked;
/// the lazy "create initial profile" init is a no-op here because the repo reports an existing profile.
/// </summary>
public class ProfileServiceTests
{
    private readonly Mock<IProfileRepository> _repo = new();
    private readonly Mock<IFileHelper> _fileHelper = new();
    private readonly Mock<IGlobalPathService> _globalPaths = new();
    private readonly ProfileService _service;

    public ProfileServiceTests()
    {
        _repo.SetReturnsDefault(Task.CompletedTask); // void-returning Task methods (Create/Update/Delete/Save/SetActive)
        // Non-empty list → EnsureInitialProfileExistsAsync does not auto-create a default profile.
        _repo.Setup(r => r.GetAllProfilesAsync()).ReturnsAsync(new List<D3dxSkinManager.Modules.Profiles.Models.Profile>
        {
            new() { Id = "existing", Name = "Existing" },
        });
        _globalPaths.Setup(p => p.GetProfileDirectoryPath(It.IsAny<string>())).Returns("C:\\profiles\\x");
        _globalPaths.Setup(p => p.GetProfileThumbnailsDirectory(It.IsAny<string>())).Returns("C:\\profiles\\x\\thumbs");
        _fileHelper.Setup(f => f.CreateDirectoryAsync(It.IsAny<string>())).ReturnsAsync(true);

        _service = new ProfileService(
            _globalPaths.Object,
            _fileHelper.Object,
            new Mock<IPathHelper>().Object,
            new Mock<IHashHelper>().Object,
            new Mock<IImageHelper>().Object,
            _repo.Object,
            new Mock<ILogHelper>().Object);
    }

    [Fact]
    public async Task CreateProfileAsync_CreatesDirectory_PersistsProfileAndDefaultConfig()
    {
        var created = await _service.CreateProfileAsync(new CreateProfileRequest { Name = "Test", Color = "#abcdef" });

        created.Name.Should().Be("Test");
        created.Color.Should().Be("#abcdef");
        _fileHelper.Verify(f => f.CreateDirectoryAsync(It.IsAny<string>()), Times.AtLeastOnce);
        _repo.Verify(r => r.CreateProfileAsync(It.Is<D3dxSkinManager.Modules.Profiles.Models.Profile>(p => p.Name == "Test")), Times.Once);
        _repo.Verify(r => r.SaveProfileConfigurationAsync(It.IsAny<string>(), It.IsAny<ProfileConfiguration>()), Times.Once);
    }

    [Fact]
    public async Task GetProfileByIdAsync_DelegatesToRepository()
    {
        _repo.Setup(r => r.GetProfileAsync("p1")).ReturnsAsync(new D3dxSkinManager.Modules.Profiles.Models.Profile { Id = "p1", Name = "One" });

        (await _service.GetProfileByIdAsync("p1"))!.Id.Should().Be("p1");
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenNotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetProfileAsync("missing")).ReturnsAsync((D3dxSkinManager.Modules.Profiles.Models.Profile?)null);

        (await _service.UpdateProfileAsync(new UpdateProfileRequest { ProfileId = "missing", Name = "X" })).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateProfileAsync_WhenFound_AppliesChangesAndReturnsTrue()
    {
        _repo.Setup(r => r.GetProfileAsync("p1")).ReturnsAsync(new D3dxSkinManager.Modules.Profiles.Models.Profile { Id = "p1", Name = "Old" });

        var ok = await _service.UpdateProfileAsync(new UpdateProfileRequest { ProfileId = "p1", Name = "New" });

        ok.Should().BeTrue();
        _repo.Verify(r => r.UpdateProfileAsync(It.Is<D3dxSkinManager.Modules.Profiles.Models.Profile>(p => p.Name == "New")), Times.Once);
    }

    [Fact]
    public async Task DeleteProfileAsync_WhenActive_Throws()
    {
        _repo.Setup(r => r.GetActiveProfileIdAsync()).ReturnsAsync("active");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteProfileAsync("active"));
        _repo.Verify(r => r.DeleteProfileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteProfileAsync_WhenNonActiveAndExists_DeletesAndReturnsTrue()
    {
        _repo.Setup(r => r.GetActiveProfileIdAsync()).ReturnsAsync("active");
        _repo.Setup(r => r.GetProfileAsync("p1")).ReturnsAsync(new D3dxSkinManager.Modules.Profiles.Models.Profile { Id = "p1", Name = "One" });

        (await _service.DeleteProfileAsync("p1")).Should().BeTrue();
        _repo.Verify(r => r.DeleteProfileAsync("p1"), Times.Once);
    }

    [Fact]
    public async Task DeleteProfileAsync_WhenNotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetActiveProfileIdAsync()).ReturnsAsync("active");
        _repo.Setup(r => r.GetProfileAsync("missing")).ReturnsAsync((D3dxSkinManager.Modules.Profiles.Models.Profile?)null);

        (await _service.DeleteProfileAsync("missing")).Should().BeFalse();
    }

    [Fact]
    public async Task SwitchProfileAsync_SetsActiveProfile()
    {
        (await _service.SwitchProfileAsync("p2")).Should().BeTrue();
        _repo.Verify(r => r.SetActiveProfileIdAsync("p2"), Times.Once);
    }

    // ===== ApplyConfigurationUpdateAsync — merge/normalize/clamp moved out of ProfileFacade =====

    /// <summary>Capture the persisted config + let each test seed the existing config to merge onto.</summary>
    private (Func<ProfileConfiguration?> Saved, Action<ProfileConfiguration?> SetExisting) ArrangeCapture()
    {
        ProfileConfiguration? saved = null;
        _repo.Setup(r => r.SaveProfileConfigurationAsync("p1", It.IsAny<ProfileConfiguration>()))
            .Callback<string, ProfileConfiguration>((_, c) => saved = c)
            .Returns(Task.CompletedTask);
        void SetExisting(ProfileConfiguration? cfg) =>
            _repo.Setup(r => r.GetProfileConfigurationAsync("p1")).ReturnsAsync(cfg);
        return (() => saved, SetExisting);
    }

    [Theory]
    [InlineData(500, 100)]  // above max → clamped to 100
    [InlineData(0, 1)]      // below min → clamped to 1
    [InlineData(42, 42)]    // in range → unchanged
    public async Task ApplyConfigurationUpdate_ClampsCleanupMaxCaches(int input, int expected)
    {
        var (saved, setExisting) = ArrangeCapture();
        setExisting(new ProfileConfiguration { ProfileId = "p1" });

        await _service.ApplyConfigurationUpdateAsync("p1", new ProfileConfigUpdate { CleanupMaxCaches = input });

        saved()!.ModWork.CleanupMaxCaches.Should().Be(expected);
    }

    [Fact]
    public async Task ApplyConfigurationUpdate_NormalizesWorkModeToLowercase_AndStoresDirForCustomModes()
    {
        var (saved, setExisting) = ArrangeCapture();
        setExisting(new ProfileConfiguration { ProfileId = "p1" });

        await _service.ApplyConfigurationUpdateAsync("p1",
            new ProfileConfigUpdate { WorkMode = "External", WorkDirectory = "D:/mods" });

        saved()!.ModWork.Mode.Should().Be("external");
        saved()!.ModWork.Directory.Should().Be("D:/mods");
    }

    [Fact]
    public async Task ApplyConfigurationUpdate_InternalMode_NullsCustomDirectory()
    {
        var (saved, setExisting) = ArrangeCapture();
        setExisting(new ProfileConfiguration
        {
            ProfileId = "p1",
            ModWork = new ModWorkConfiguration { Mode = "external", Directory = "D:/old" },
        });

        await _service.ApplyConfigurationUpdateAsync("p1", new ProfileConfigUpdate { WorkMode = "internal" });

        saved()!.ModWork.Mode.Should().Be("internal");
        saved()!.ModWork.Directory.Should().BeNull();
    }

    [Theory]
    [InlineData(999, 120)]  // above max → 120
    [InlineData(0, 1)]      // below min → 1
    public async Task ApplyConfigurationUpdate_ClampsFixToolsTimeout(int input, int expected)
    {
        var (saved, setExisting) = ArrangeCapture();
        setExisting(new ProfileConfiguration { ProfileId = "p1" });

        await _service.ApplyConfigurationUpdateAsync("p1", new ProfileConfigUpdate { FixToolsTimeoutMinutes = input });

        saved()!.FixTools.TimeoutMinutes.Should().Be(expected);
    }

    [Fact]
    public async Task ApplyConfigurationUpdate_PartialUpdate_PreservesUntouchedFields()
    {
        var (saved, setExisting) = ArrangeCapture();
        setExisting(new ProfileConfiguration
        {
            ProfileId = "p1",
            Launch = new LaunchConfiguration { Path = "game.exe", Args = "--foo" },
        });

        // Only change compression — Launch must survive untouched.
        await _service.ApplyConfigurationUpdateAsync("p1", new ProfileConfigUpdate { CompressionType = "zip" });

        saved()!.ModImport.CompressionType.Should().Be("zip");
        saved()!.Launch.Path.Should().Be("game.exe");
        saved()!.Launch.Args.Should().Be("--foo");
    }

    [Fact]
    public async Task ApplyConfigurationUpdate_WhenNoExistingConfig_CreatesOneForTheProfile()
    {
        var (saved, setExisting) = ArrangeCapture();
        setExisting(null); // repo has no config yet

        var ok = await _service.ApplyConfigurationUpdateAsync("p1", new ProfileConfigUpdate { CompressionMode = "ultra" });

        ok.Should().BeTrue();
        saved()!.ProfileId.Should().Be("p1");
        saved()!.ModImport.CompressionMode.Should().Be("ultra");
    }
}
