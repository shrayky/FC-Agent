using Domain.DotNet;

namespace Domain.Tests;

[TestFixture]
public class DotNetRuntimeRequirementTests
{
    /// <summary>
    /// Патч 10.0.2 закрывает требование Microsoft.AspNetCore.App/10.0.
    /// </summary>
    [Test]
    public void IsSatisfied_true_для_патча_10_0_2()
    {
        Assert.That(
            DotNetRuntimeRequirement.IsSatisfied(
                ["Microsoft.AspNetCore.App/10.0.2"],
                "Microsoft.AspNetCore.App/10.0"),
            Is.True);
    }

    /// <summary>
    /// Пустой список runtime не закрывает требование 10.0.
    /// </summary>
    [Test]
    public void IsSatisfied_false_если_runtime_нет()
    {
        Assert.That(
            DotNetRuntimeRequirement.IsSatisfied([], "Microsoft.AspNetCore.App/10.0"),
            Is.False);
    }
}
