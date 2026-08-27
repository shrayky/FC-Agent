using System.Runtime.InteropServices;
using Domain.Agent;

namespace Domain.Tests;

[TestFixture]
public class AgentArchitectureTests
{
    /// <summary>
    /// win-x86 процесс должен сообщать x86, иначе командир не увидит пакеты Nuke.
    /// </summary>
    [Test]
    public void Name_для_X86_это_x86()
    {
        Assert.That(AgentArchitecture.Name(Architecture.X86), Is.EqualTo("x86"));
    }

    /// <summary>
    /// 64-битный процесс — x64.
    /// </summary>
    [Test]
    public void Name_для_X64_это_x64()
    {
        Assert.That(AgentArchitecture.Name(Architecture.X64), Is.EqualTo("x64"));
    }
}
