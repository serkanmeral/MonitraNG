using System.Text.Json;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Trees;
using Xunit;

namespace MngDataGateway.Tests.Services;

public class TreeWriteRulesTests
{
    [Fact]
    public void Blank_reference_is_empty()
    {
        Assert.True(TreeWriteRules.TryNormalizeReference("  ", out var id, out var many));
        Assert.Null(id);
        Assert.False(many);
    }

    [Fact]
    public void One_item_array_is_a_single_reference()
    {
        using var document = JsonDocument.Parse("""["user-1"]""");
        Assert.True(TreeWriteRules.TryNormalizeReference(document.RootElement, out var id, out var many));
        Assert.Equal("user-1", id);
        Assert.False(many);
    }

    [Fact]
    public void Two_item_array_is_rejected()
    {
        Assert.False(TreeWriteRules.TryNormalizeReference(new[] { "a", "b" }, out _, out var many));
        Assert.True(many);
    }

    [Fact]
    public void Walk_accepts_a_chain_that_ends()
    {
        var parents = new Dictionary<string, string?> { ["b"] = "a", ["a"] = null };
        var status = TreeWriteRules.WalkParents("c", "b", id => parents.TryGetValue(id, out var next) ? next : null);
        Assert.Equal(TreeWalkStatus.Ok, status);
    }

    [Fact]
    public void Walk_rejects_self_and_a_loop()
    {
        Assert.Equal(TreeWalkStatus.Cycle, TreeWriteRules.WalkParents("a", "a", _ => null));

        var parents = new Dictionary<string, string?> { ["a"] = "b", ["b"] = "a" };
        var status = TreeWriteRules.WalkParents("b", "a", id => parents[id]);
        Assert.Equal(TreeWalkStatus.Cycle, status);
    }

    [Fact]
    public void Walk_rejects_a_chain_past_the_depth_cap()
    {
        var status = TreeWriteRules.WalkParents(null, "n0", id =>
        {
            var number = int.Parse(id[1..]);
            return "n" + (number + 1);
        });
        Assert.Equal(TreeWalkStatus.TooDeep, status);
    }

    [Fact]
    public void Blank_parent_is_removed_and_marks_the_single_root()
    {
        var tree = new TreeConstraint { parentField = "ust", occupantField = "oturan", singleRoot = true };
        var data = new Dictionary<string, object> { ["ad"] = "Genel Müdür", ["ust"] = "", ["oturan"] = "  " };
        var removed = TreeWriteRules.RemoveBlankReferences(tree, data);
        Assert.Contains("ust", removed);
        Assert.Contains("oturan", removed);
        Assert.False(data.ContainsKey("ust"));

        TreeWriteRules.ApplyRootMarker(tree, data, parentTouched: true, parentEmpty: true, unsets: null);
        Assert.Equal(TreeWriteRules.RootMarkerValue, data[TreeWriteRules.RootMarkerField]);
    }

    [Fact]
    public void Empty_staff_list_is_removed_and_a_repeated_person_is_rejected()
    {
        var tree = new TreeConstraint { parentField = "ust", staffField = "calisanlar" };
        var data = new Dictionary<string, object> { ["calisanlar"] = Array.Empty<string>() };
        var removed = TreeWriteRules.RemoveBlankReferences(tree, data);
        Assert.Contains("calisanlar", removed);
        Assert.False(data.ContainsKey("calisanlar"));

        Assert.True(TreeWriteRules.TryReadPersonIds(new[] { "a", "b" }, out var ids, out var duplicate));
        Assert.False(duplicate);
        Assert.Equal(new[] { "a", "b" }, ids);

        Assert.False(TreeWriteRules.TryReadPersonIds(new[] { "a", "a" }, out _, out duplicate));
        Assert.True(duplicate);
    }

    [Fact]
    public void Child_clears_the_root_marker()
    {
        var tree = new TreeConstraint { parentField = "ust", singleRoot = true };
        var data = new Dictionary<string, object> { ["ust"] = "root-1", [TreeWriteRules.RootMarkerField] = "root" };
        var unsets = new HashSet<string>();
        TreeWriteRules.ApplyRootMarker(tree, data, parentTouched: true, parentEmpty: false, unsets);
        Assert.False(data.ContainsKey(TreeWriteRules.RootMarkerField));
        Assert.Contains(TreeWriteRules.RootMarkerField, unsets);
    }
}
