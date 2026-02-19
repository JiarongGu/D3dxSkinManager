using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using D3dxSkinManager.Modules.Migration.Services;
using D3dxSkinManager.Modules.Migration.Models;
using D3dxSkinManager.Modules.Mods.Models;
using D3dxSkinManager.Modules.Mods.Services;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Tools.Services;

namespace D3dxSkinManager.Tests.Modules.Migration;

/// <summary>
/// Unit tests for MigrationService
/// Tests classification migration with hierarchical structure based on real data
/// Real data structure: E:\Mods\Endfield MOD\home\Endfield\classification\
/// </summary>
public class MigrationServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly string _testClassificationPath;
    private readonly Mock<IProfileContext> _mockProfileContext;
    private readonly Mock<IModRepository> _mockModRepository;
    private readonly Mock<IClassificationRepository> _mockClassificationRepository;
    private readonly Mock<IClassificationThumbnailService> _mockThumbnailService;
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IModManagementService> _mockModManagementService;
    private readonly MigrationService _service;

    public MigrationServiceTests()
    {
        // Create temporary test directory with proper Python structure
        _testDataPath = Path.Combine(Path.GetTempPath(), $"d3dx_test_{Guid.NewGuid():N}");

        // Create required directory structure (matching Python installation)
        Directory.CreateDirectory(_testDataPath);
        Directory.CreateDirectory(Path.Combine(_testDataPath, "resources")); // Required for validation
        Directory.CreateDirectory(Path.Combine(_testDataPath, "home"));

        // Classification path should be under home/envName/classification
        var envPath = Path.Combine(_testDataPath, "home", "TestEnv");
        Directory.CreateDirectory(envPath);
        _testClassificationPath = Path.Combine(envPath, "classification");
        Directory.CreateDirectory(_testClassificationPath);

        // Setup mocks
        _mockProfileContext = new Mock<IProfileContext>();
        _mockProfileContext.Setup(x => x.ProfilePath).Returns(_testDataPath);

        _mockModRepository = new Mock<IModRepository>();
        _mockClassificationRepository = new Mock<IClassificationRepository>();
        _mockThumbnailService = new Mock<IClassificationThumbnailService>();
        _mockFileService = new Mock<IFileService>();
        _mockImageService = new Mock<IImageService>();
        _mockConfigService = new Mock<IConfigurationService>();
        _mockModManagementService = new Mock<IModManagementService>();

        // Setup image service to return common extensions
        _mockImageService.Setup(s => s.GetSupportedImageExtensions())
            .Returns(new[] { ".png", ".jpg", ".jpeg" });

        // Setup thumbnail service to return 0 (no thumbnails in tests)
        _mockThumbnailService.Setup(s => s.AssociateThumbnailsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        // Create service instance
        _service = new MigrationService(
            _mockProfileContext.Object,
            _mockModRepository.Object,
            _mockClassificationRepository.Object,
            _mockThumbnailService.Object,
            _mockFileService.Object,
            _mockImageService.Object,
            _mockConfigService.Object,
            _mockModManagementService.Object
        );
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
    }

    #region Classification Migration Tests

    [Fact]
    public async Task MigrateClassifications_WithRealDataStructure_ShouldCreateHierarchicalNodes()
    {
        // Arrange - Create classification files based on real data structure
        // Real file: 干员·灼热 (Fire Operator)
        var fireOperatorFile = Path.Combine(_testClassificationPath, "干员·灼热");
        await File.WriteAllLinesAsync(fireOperatorFile, new[]
        {
            "莱万汀",  // Leviathan
            "伊芙利特"  // Ifrit
        });

        // Real file: 干员·寒冷 (Cold Operator)
        var coldOperatorFile = Path.Combine(_testClassificationPath, "干员·寒冷");
        await File.WriteAllLinesAsync(coldOperatorFile, new[]
        {
            "冰雪",    // Ice
            "霜降"     // Frost
        });

        // Setup mock to track node insertions
        var insertedNodes = new List<ClassificationNode>();
        _mockClassificationRepository
            .Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>()))
            .Callback<ClassificationNode>(node => insertedNodes.Add(node))
            .ReturnsAsync((ClassificationNode node) => node);

        _mockClassificationRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        // Setup mod repository to return empty (no mods yet)
        _mockModRepository
            .Setup(r => r.GetByCategoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ModInfo>());

        // Create options with classification migration enabled
        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv", // Specify the test environment
            MigrateClassifications = true,
            MigrateMetadata = false,
            MigrateArchives = false,
            MigratePreviews = false,
            MigrateConfiguration = false
        };

        // Act
        var result = await _service.MigrateAsync(options);

        // Assert
        result.Success.Should().BeTrue();
        insertedNodes.Should().HaveCount(6); // 2 parents + 4 children

        // Verify parent nodes
        var parentNodes = insertedNodes.Where(n => n.ParentId == null).ToList();
        parentNodes.Should().HaveCount(2);
        parentNodes.Should().Contain(n => n.Name == "干员·灼热");
        parentNodes.Should().Contain(n => n.Name == "干员·寒冷");

        // Verify child nodes for Fire Operators
        var fireChildren = insertedNodes.Where(n => n.ParentId == "干员·灼热").ToList();
        fireChildren.Should().HaveCount(2);
        fireChildren.Should().Contain(n => n.Name == "莱万汀");
        fireChildren.Should().Contain(n => n.Name == "伊芙利特");

        // Verify child node IDs are simple names (not paths)
        var leviathanNode = insertedNodes.FirstOrDefault(n => n.Name == "莱万汀");
        leviathanNode.Should().NotBeNull();
        leviathanNode!.Id.Should().Be("莱万汀"); // Simple ID, not path-based
        leviathanNode.ParentId.Should().Be("干员·灼热");
    }

    [Fact]
    public async Task MigrateClassifications_WithModsMatchingCategories_ShouldLinkModsToChildNodes()
    {
        // Arrange - Create classification file
        var categoryFile = Path.Combine(_testClassificationPath, "测试分类");
        await File.WriteAllLinesAsync(categoryFile, new[] { "测试角色" });

        // Create mock mods with matching category
        var matchingMods = new List<ModInfo>
        {
            new() { SHA = "ABC123", Category = "测试角色", Name = "测试角色-皮肤1", Tags = new List<string>() },
            new() { SHA = "DEF456", Category = "测试角色", Name = "测试角色-皮肤2", Tags = new List<string>() }
        };

        _mockModRepository
            .Setup(r => r.GetByCategoryAsync("测试角色"))
            .ReturnsAsync(matchingMods);

        _mockClassificationRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockClassificationRepository
            .Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>()))
            .ReturnsAsync((ClassificationNode node) => node);

        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv",
            MigrateClassifications = true
        };

        // Act
        await _service.MigrateAsync(options);

        // Assert - Mods are linked via Category field, not tags
        // No update calls should be made (mods already have correct Category field)
        _mockModRepository.Verify(
            r => r.UpdateAsync(It.IsAny<ModInfo>()),
            Times.Never);
    }

    [Fact]
    public async Task MigrateClassifications_WithEmptyLines_ShouldSkipEmptyLines()
    {
        // Arrange
        var categoryFile = Path.Combine(_testClassificationPath, "测试");
        await File.WriteAllLinesAsync(categoryFile, new[]
        {
            "角色1",
            "",           // Empty line
            "   ",        // Whitespace only
            "角色2"
        });

        var insertedNodes = new List<ClassificationNode>();
        _mockClassificationRepository
            .Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>()))
            .Callback<ClassificationNode>(node => insertedNodes.Add(node))
            .ReturnsAsync((ClassificationNode node) => node);

        _mockClassificationRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockModRepository
            .Setup(r => r.GetByCategoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ModInfo>());

        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv",
            MigrateClassifications = true
        };

        // Act
        await _service.MigrateAsync(options);

        // Assert - Should only create nodes for non-empty lines
        var childNodes = insertedNodes.Where(n => n.ParentId != null).ToList();
        childNodes.Should().HaveCount(2);
        childNodes.Should().Contain(n => n.Name == "角色1");
        childNodes.Should().Contain(n => n.Name == "角色2");
    }

    [Fact]
    public async Task MigrateClassifications_WithExistingNodes_ShouldNotDuplicateNodes()
    {
        // Arrange
        var categoryFile = Path.Combine(_testClassificationPath, "已存在分类");
        await File.WriteAllLinesAsync(categoryFile, new[] { "已存在角色" });

        // Simulate that parent node already exists
        _mockClassificationRepository
            .Setup(r => r.ExistsAsync("已存在分类"))
            .ReturnsAsync(true);

        // Child node doesn't exist
        _mockClassificationRepository
            .Setup(r => r.ExistsAsync("已存在角色"))
            .ReturnsAsync(false);

        var insertedNodes = new List<ClassificationNode>();
        _mockClassificationRepository
            .Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>()))
            .Callback<ClassificationNode>(node => insertedNodes.Add(node))
            .ReturnsAsync((ClassificationNode node) => node);

        _mockModRepository
            .Setup(r => r.GetByCategoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ModInfo>());

        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv",
            MigrateClassifications = true
        };

        // Act
        await _service.MigrateAsync(options);

        // Assert - Should only insert child node, not parent
        insertedNodes.Should().HaveCount(1);
        insertedNodes[0].Name.Should().Be("已存在角色");
    }

    [Fact]
    public async Task MigrateClassifications_WithNoModsMatchingCategory_ShouldLogInfoAndContinue()
    {
        // Arrange
        var categoryFile = Path.Combine(_testClassificationPath, "无匹配");
        await File.WriteAllLinesAsync(categoryFile, new[] { "不存在的角色" });

        _mockModRepository
            .Setup(r => r.GetByCategoryAsync("不存在的角色"))
            .ReturnsAsync(new List<ModInfo>()); // No matching mods

        _mockClassificationRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockClassificationRepository
            .Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>()))
            .ReturnsAsync((ClassificationNode node) => node);

        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv",
            MigrateClassifications = true
        };

        // Act
        var result = await _service.MigrateAsync(options);

        // Assert - Should succeed even without matching mods
        result.Success.Should().BeTrue();
        result.ClassificationRulesCreated.Should().Be(2); // 1 parent + 1 child
    }

    [Fact]
    public async Task MigrateClassifications_WithMultipleCategories_ShouldCreateSeparateHierarchies()
    {
        // Arrange - Create multiple classification categories
        var categories = new Dictionary<string, string[]>
        {
            { "干员·灼热", new[] { "莱万汀", "伊芙利特" } },
            { "干员·寒冷", new[] { "冰雪" } },
            { "怪物", new[] { "史莱姆", "哥布林" } }
        };

        foreach (var (category, objects) in categories)
        {
            var filePath = Path.Combine(_testClassificationPath, category);
            await File.WriteAllLinesAsync(filePath, objects);
        }

        var insertedNodes = new List<ClassificationNode>();
        _mockClassificationRepository
            .Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>()))
            .Callback<ClassificationNode>(node => insertedNodes.Add(node))
            .ReturnsAsync((ClassificationNode node) => node);

        _mockClassificationRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockModRepository
            .Setup(r => r.GetByCategoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ModInfo>());

        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv",
            MigrateClassifications = true
        };

        // Act
        await _service.MigrateAsync(options);

        // Assert
        var parentNodes = insertedNodes.Where(n => n.ParentId == null).ToList();
        parentNodes.Should().HaveCount(3);

        var fireOperatorChildren = insertedNodes.Where(n => n.ParentId == "干员·灼热").ToList();
        fireOperatorChildren.Should().HaveCount(2);

        var coldOperatorChildren = insertedNodes.Where(n => n.ParentId == "干员·寒冷").ToList();
        coldOperatorChildren.Should().HaveCount(1);

        var monsterChildren = insertedNodes.Where(n => n.ParentId == "怪物").ToList();
        monsterChildren.Should().HaveCount(2);
    }

    [Fact]
    public async Task MigrateClassifications_WithNoClassificationDirectory_ShouldLogWarningAndContinue()
    {
        // Arrange - Delete classification directory
        Directory.Delete(_testClassificationPath, true);

        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv",
            MigrateClassifications = true
        };

        // Act
        var result = await _service.MigrateAsync(options);

        // Assert - Should succeed but with 0 classification rules
        result.Success.Should().BeTrue();
        result.ClassificationRulesCreated.Should().Be(0);
    }

    [Fact]
    public async Task MigrateClassifications_NodePriorities_ShouldSetCorrectly()
    {
        // Arrange
        var categoryFile = Path.Combine(_testClassificationPath, "测试优先级");
        await File.WriteAllLinesAsync(categoryFile, new[] { "角色" });

        var insertedNodes = new List<ClassificationNode>();
        _mockClassificationRepository
            .Setup(r => r.InsertAsync(It.IsAny<ClassificationNode>()))
            .Callback<ClassificationNode>(node => insertedNodes.Add(node))
            .ReturnsAsync((ClassificationNode node) => node);

        _mockClassificationRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockModRepository
            .Setup(r => r.GetByCategoryAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ModInfo>());

        var options = new MigrationOptions
        {
            SourcePath = _testDataPath,
            EnvironmentName = "TestEnv",
            MigrateClassifications = true
        };

        // Act
        await _service.MigrateAsync(options);

        // Assert - Verify priorities
        var parentNode = insertedNodes.First(n => n.ParentId == null);
        parentNode.Priority.Should().Be(100); // Parent priority

        var childNode = insertedNodes.First(n => n.ParentId != null);
        childNode.Priority.Should().Be(50); // Child priority
    }

    #endregion

    #region Helper Methods

    private void CreateMockModsIndex(string envPath, Dictionary<string, object> modsData)
    {
        var modsIndexDir = Path.Combine(envPath, "modsIndex");
        Directory.CreateDirectory(modsIndexDir);

        var indexFile = Path.Combine(modsIndexDir, "index_2026-02.json");
        var json = System.Text.Json.JsonSerializer.Serialize(new { mods = modsData });
        File.WriteAllText(indexFile, json);
    }

    #endregion
}
