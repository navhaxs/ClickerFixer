using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickerFixer.Desktop.Services;

internal static class TargetPriorityOrdering
{
    public static List<T> Apply<T>(List<T> targets, List<string>? priorityOrder, Func<T, string> nameSelector)
    {
        priorityOrder ??= new List<string>();

        return targets
            .Select((item, originalIndex) => (item, originalIndex))
            .OrderBy(x =>
            {
                var priorityIndex = priorityOrder.IndexOf(nameSelector(x.item));
                return priorityIndex >= 0 ? priorityIndex : int.MaxValue;
            })
            .ThenBy(x => x.originalIndex)
            .Select(x => x.item)
            .ToList();
    }
}
