namespace HostApp.Tests;

[TestFixture]
public class ProductProcessPolicyTests
{
    [Test]
    public void fc_agent_обязателен_в_пакете()
    {
        Assert.That(HostApp.Services.ProductProcessPolicy.IsRequiredInPackage("fc-agent"), Is.True);
    }

    [Test]
    public void fc_remote_не_обязателен_в_пакете()
    {
        Assert.That(HostApp.Services.ProductProcessPolicy.IsRequiredInPackage("fc-remote"), Is.False);
    }
}
