using Crap4DotNet.Core.Models;

namespace Crap4DotNet.Core.Calculation;

/// <summary>
/// Groups flat method results into namespace → class hierarchy with stats at each level.
/// </summary>
public static class HierarchyBuilder
{
    public static IReadOnlyList<NamespaceCrapData> Build(IReadOnlyList<MethodCrapData> methods)
    {
        return methods
            .GroupBy(m => m.Identity.Namespace)
            .Select(nsGroup =>
            {
                var nsMethods = nsGroup.ToList();
                return new NamespaceCrapData
                {
                    Name = nsGroup.Key,
                    Stats = CrapStatisticsCalculator.Calculate(nsMethods),
                    Classes = nsGroup
                        .GroupBy(m => m.Identity.ClassName)
                        .Select(classGroup =>
                        {
                            var classMethods = classGroup.ToList();
                            return new TypeCrapData
                            {
                                Name = classGroup.Key,
                                Stats = CrapStatisticsCalculator.Calculate(classMethods),
                                Methods = classMethods
                            };
                        })
                        .ToList()
                };
            })
            .ToList();
    }
}
