using ClickerFixer.Desktop.Services;

namespace ClickerFixer.Desktop.Tests;

public class TargetPriorityOrderingTests
{
    [Fact]
    public void Apply_ReordersByPriorityList()
    {
        var targets = new List<string> { "A", "B", "C", "D" };
        var priority = new List<string> { "C", "A", "D", "B" };

        var result = TargetPriorityOrdering.Apply(targets, priority, name => name);

        Assert.Equal(new List<string> { "C", "A", "D", "B" }, result);
    }

    [Fact]
    public void Apply_UnlistedItemsGoToEndInOriginalOrder()
    {
        var targets = new List<string> { "A", "B", "C", "D" };
        var priority = new List<string> { "C" };

        var result = TargetPriorityOrdering.Apply(targets, priority, name => name);

        Assert.Equal(new List<string> { "C", "A", "B", "D" }, result);
    }

    [Fact]
    public void Apply_UnknownNamesInPriorityListAreIgnored()
    {
        var targets = new List<string> { "A", "B" };
        var priority = new List<string> { "NotReal", "B", "AlsoNotReal", "A" };

        var result = TargetPriorityOrdering.Apply(targets, priority, name => name);

        Assert.Equal(new List<string> { "B", "A" }, result);
    }

    [Fact]
    public void Apply_EmptyPriorityList_PreservesOriginalOrder()
    {
        var targets = new List<string> { "A", "B", "C" };

        var result = TargetPriorityOrdering.Apply(targets, new List<string>(), name => name);

        Assert.Equal(new List<string> { "A", "B", "C" }, result);
    }

    [Fact]
    public void Apply_NullPriorityList_PreservesOriginalOrder()
    {
        var targets = new List<string> { "A", "B", "C" };

        var result = TargetPriorityOrdering.Apply(targets, null, name => name);

        Assert.Equal(new List<string> { "A", "B", "C" }, result);
    }
}
