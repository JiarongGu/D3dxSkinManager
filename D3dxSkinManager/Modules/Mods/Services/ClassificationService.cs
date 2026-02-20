using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using D3dxSkinManager.Modules.Core.Services;
using D3dxSkinManager.Modules.Mods.Models;

namespace D3dxSkinManager.Modules.Mods.Services;

/// <summary>
/// Interface for classification service
/// </summary>
public interface IClassificationService
{
    Task<List<ClassificationNode>> GetClassificationTreeAsync();
    Task<ClassificationNode?> FindClassificationForObjectAsync(string category);
    Task<bool> RefreshTreeAsync();
    Task<bool> MoveNodeAsync(string nodeId, string? newParentId, int? dropPosition = null);
    Task<bool> ReorderNodeAsync(string nodeId, int newPosition);
    Task<bool> UpdateNodeAsync(string nodeId, string name, string? icon = null);
    Task<bool> SetNodeThumbnailAsync(string nodeId, string thumbnailPath);
    Task<ClassificationNode?> GetNodeByNameAsync(string name);
    Task<bool> DeleteNodeAsync(string nodeId);
    Task<ClassificationNode?> CreateNodeAsync(string nodeId, string name, string? parentId = null, int priority = 100, string? description = null);
    Task<bool> NodeExistsAsync(string nodeId);
}

/// <summary>
/// Service for managing classification tree
/// Reads from SQLite database populated by migration
/// </summary>
public class ClassificationService : IClassificationService
{
    private readonly IClassificationRepository _repository;
    private readonly IModRepository _modRepository;
    private readonly Core.Services.IPathHelper _pathHelper;
    private List<ClassificationNode>? _cachedTree;
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public ClassificationService(
        IClassificationRepository repository,
        IModRepository modRepository,
        Core.Services.IPathHelper pathHelper)
    {
        _repository = repository;
        _modRepository = modRepository;
        _pathHelper = pathHelper;
    }

    /// <summary>
    /// Get the full classification tree with all children populated
    /// Returns cached tree if fresh enough
    /// </summary>
    public async Task<List<ClassificationNode>> GetClassificationTreeAsync()
    {
        // Return cached tree if still fresh
        if (_cachedTree != null && DateTime.Now - _lastRefresh < CacheExpiry)
        {
            return _cachedTree;
        }

        // Rebuild tree from database
        await RefreshTreeAsync();
        return _cachedTree ?? new List<ClassificationNode>();
    }

    /// <summary>
    /// Force refresh the tree from database
    /// </summary>
    public async Task<bool> RefreshTreeAsync()
    {
        try
        {
            // Get all nodes from database
            var allNodes = await _repository.GetAllAsync();

            // Build dictionary for quick lookup
            var nodeDict = allNodes.ToDictionary(n => n.Id);

            // Build tree structure by connecting parents to children
            var rootNodes = new List<ClassificationNode>();

            foreach (var node in allNodes)
            {
                if (string.IsNullOrEmpty(node.ParentId))
                {
                    // Root node
                    rootNodes.Add(node);
                }
                else if (nodeDict.TryGetValue(node.ParentId, out var parent))
                {
                    // Add to parent's children
                    parent.Children.Add(node);
                }
            }

            // Calculate mod counts for all nodes
            await CalculateModCountsAsync(rootNodes);

            _cachedTree = rootNodes;
            _lastRefresh = DateTime.Now;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Find which classification a given object name belongs to
    /// Searches for a child node matching the object name
    /// </summary>
    public async Task<ClassificationNode?> FindClassificationForObjectAsync(string category)
    {
        // Try exact match first
        var node = await _repository.GetByNameAsync(category);
        if (node != null)
        {
            return node;
        }

        // Fall back to tree search
        var tree = await GetClassificationTreeAsync();
        return FindNodeByNameRecursive(tree, category);
    }

    /// <summary>
    /// Recursively find node by name (exact match)
    /// </summary>
    private ClassificationNode? FindNodeByNameRecursive(List<ClassificationNode> nodes, string nodeName)
    {
        foreach (var node in nodes)
        {
            // Check if this node's name matches
            if (node.Name == nodeName)
            {
                return node;
            }

            // Recursively check children
            if (node.Children.Count > 0)
            {
                var match = FindNodeByNameRecursive(node.Children, nodeName);
                if (match != null)
                    return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Move a node to a new parent (or root level if newParentId is null)
    /// </summary>
    public async Task<bool> MoveNodeAsync(string nodeId, string? newParentId, int? dropPosition = null)
    {
        try
        {
            // Move the node to new parent
            var moved = await _repository.MoveNodeAsync(nodeId, newParentId);
            if (!moved) return false;

            // If dropPosition is specified, reorder siblings
            if (dropPosition.HasValue)
            {
                var siblings = await _repository.GetChildrenAsync(newParentId);
                var updates = new List<(string nodeId, int priority)>();

                // Calculate new priorities based on drop position
                int priority = siblings.Count * 100;
                for (int i = 0; i < siblings.Count; i++)
                {
                    if (i == dropPosition.Value)
                    {
                        // Insert the moved node here
                        updates.Add((nodeId, priority));
                        priority -= 100;
                    }

                    if (siblings[i].Id != nodeId)
                    {
                        updates.Add((siblings[i].Id, priority));
                        priority -= 100;
                    }
                }

                // If dropPosition is at the end
                if (dropPosition.Value >= siblings.Count)
                {
                    updates.Add((nodeId, priority));
                }

                await _repository.ReorderSiblingsAsync(updates);
            }

            // Invalidate cache
            await RefreshTreeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reorder a node within its current siblings
    /// </summary>
    public async Task<bool> ReorderNodeAsync(string nodeId, int newPosition)
    {
        try
        {
            var node = await _repository.GetByIdAsync(nodeId);
            if (node == null) return false;

            var siblings = await _repository.GetChildrenAsync(node.ParentId);
            var updates = new List<(string nodeId, int priority)>();

            // Remove the node from its current position
            var otherSiblings = siblings.Where(s => s.Id != nodeId).ToList();

            // Calculate new priorities
            int priority = (otherSiblings.Count + 1) * 100;
            for (int i = 0; i <= otherSiblings.Count; i++)
            {
                if (i == newPosition)
                {
                    updates.Add((nodeId, priority));
                    priority -= 100;
                }

                if (i < otherSiblings.Count)
                {
                    updates.Add((otherSiblings[i].Id, priority));
                    priority -= 100;
                }
            }

            await _repository.ReorderSiblingsAsync(updates);

            // Invalidate cache
            await RefreshTreeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Update a classification node's name
    /// </summary>
    public async Task<bool> UpdateNodeAsync(string nodeId, string name, string? icon = null)
    {
        try
        {
            var node = await _repository.GetByIdAsync(nodeId);
            if (node == null) return false;

            node.Name = name;
            // Note: Icon parameter is kept for API compatibility but not used in the current model

            var updated = await _repository.UpdateAsync(node);
            if (updated)
            {
                await RefreshTreeAsync();
            }

            return updated;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Delete a classification node and all its children
    /// </summary>
    public async Task<bool> DeleteNodeAsync(string nodeId)
    {
        try
        {
            // Check if node exists
            var node = await _repository.GetByIdAsync(nodeId);
            if (node == null) return false;

            // Delete all children recursively
            await DeleteNodeAndChildrenRecursiveAsync(nodeId);

            // Invalidate cache
            await RefreshTreeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Create a new classification node
    /// Returns null if node already exists
    /// </summary>
    public async Task<ClassificationNode?> CreateNodeAsync(
        string nodeId,
        string name,
        string? parentId = null,
        int priority = 100,
        string? description = null)
    {
        try
        {
            // Check if node already exists
            if (await _repository.ExistsAsync(nodeId))
            {
                return null; // Already exists
            }

            var node = new ClassificationNode
            {
                Id = nodeId,
                Name = name,
                ParentId = parentId,
                Thumbnail = null,
                Priority = priority,
                Description = description ?? $"Node: {name}",
                Children = new List<ClassificationNode>()
            };

            await _repository.InsertAsync(node);
            await RefreshTreeAsync(); // Invalidate cache
            return node;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Set thumbnail for a classification node
    /// </summary>
    public async Task<bool> SetNodeThumbnailAsync(string nodeId, string thumbnailPath)
    {
        try
        {
            var node = await _repository.GetByIdAsync(nodeId);
            if (node == null)
                return false;

            // Convert to relative path if under data folder for portability
            var relativeThumbnailPath = _pathHelper.ToRelativePath(thumbnailPath) ?? thumbnailPath;

            node.Thumbnail = relativeThumbnailPath;
            var updated = await _repository.UpdateAsync(node);

            if (updated)
            {
                await RefreshTreeAsync(); // Invalidate cache
            }

            return updated;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get classification node by name (useful for migration thumbnail association)
    /// </summary>
    public async Task<ClassificationNode?> GetNodeByNameAsync(string name)
    {
        return await _repository.GetByNameAsync(name);
    }

    /// <summary>
    /// Check if a classification node exists
    /// </summary>
    public async Task<bool> NodeExistsAsync(string nodeId)
    {
        return await _repository.ExistsAsync(nodeId);
    }

    /// <summary>
    /// Recursively delete a node and all its children
    /// </summary>
    private async Task DeleteNodeAndChildrenRecursiveAsync(string nodeId)
    {
        // Get all children
        var children = await _repository.GetChildrenAsync(nodeId);

        // Recursively delete children first
        foreach (var child in children)
        {
            await DeleteNodeAndChildrenRecursiveAsync(child.Id);
        }

        // Delete the node itself
        await _repository.DeleteAsync(nodeId);
    }

    /// <summary>
    /// Calculate mod counts for all nodes recursively
    /// Each node's ModCount = direct mods + all descendant mods
    /// </summary>
    private async Task CalculateModCountsAsync(List<ClassificationNode> nodes)
    {
        // Get all mods from database once
        var allMods = await _modRepository.GetAllAsync();

        // Group mods by category for quick lookup
        var modsByCategory = allMods
            .GroupBy(m => m.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        // Calculate counts recursively for each root node
        foreach (var node in nodes)
        {
            CalculateNodeModCount(node, modsByCategory);
        }
    }

    /// <summary>
    /// Recursively calculate mod count for a node and all its descendants
    /// Returns the total count (node's mods + all descendant mods)
    /// </summary>
    private int CalculateNodeModCount(ClassificationNode node, Dictionary<string, int> modsByCategory)
    {
        // Get direct mod count for this node's category
        var directCount = modsByCategory.TryGetValue(node.Id, out var count) ? count : 0;

        // Recursively calculate counts for all children
        var childrenCount = 0;
        foreach (var child in node.Children)
        {
            childrenCount += CalculateNodeModCount(child, modsByCategory);
        }

        // Total count is direct + children
        node.ModCount = directCount + childrenCount;
        return node.ModCount;
    }
}
