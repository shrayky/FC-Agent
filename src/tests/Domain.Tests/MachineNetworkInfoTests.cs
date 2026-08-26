using System.Net;
using Domain.Agent;

namespace Domain.Tests;

[TestFixture]
public class MachineNetworkInfoTests
{
    /// <summary>
    /// Loopback и link-local не показываем в карточке агента.
    /// </summary>
    [Test]
    public void SelectAddresses_отбрасывает_loopback_и_link_local()
    {
        var selected = MachineNetworkInfo.SelectAddresses(
        [
            IPAddress.Parse("127.0.0.1"),
            IPAddress.IPv6Loopback,
            IPAddress.Parse("169.254.10.1"),
            IPAddress.Parse("fe80::1"),
            IPAddress.Parse("192.168.1.10"),
            IPAddress.Parse("10.0.0.5")
        ]);

        Assert.That(selected, Is.EqualTo(new[] { "192.168.1.10", "10.0.0.5" }));
    }

    /// <summary>
    /// IPv4 идут раньше IPv6, дубликаты убираются.
    /// </summary>
    [Test]
    public void SelectAddresses_ipv4_сначала_без_дублей()
    {
        var selected = MachineNetworkInfo.SelectAddresses(
        [
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("192.168.0.2"),
            IPAddress.Parse("192.168.0.2")
        ]);

        Assert.That(selected, Is.EqualTo(new[] { "192.168.0.2", "2001:db8::1" }));
    }
}
