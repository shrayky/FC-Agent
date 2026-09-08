using Application.Agent;
using Domain.Agent.Interfaces;
using Moq;

namespace FrontolDatabase.Tests;

[TestFixture]
public class FcRemoteRestarterTests
{
    [Test]
    public void Restart_нет_процессов_успех_без_Kill()
    {
        var source = new Mock<IFcRemoteProcessSource>();
        source.Setup(s => s.ListByName("fc-remote")).Returns([]);
        var sut = new FcRemoteRestarter(source.Object);

        var result = sut.Restart();

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Restart_убивает_каждый_найденный_процесс()
    {
        var p1 = new Mock<IFcRemoteProcess>();
        var p2 = new Mock<IFcRemoteProcess>();
        var source = new Mock<IFcRemoteProcessSource>();
        source.Setup(s => s.ListByName("fc-remote")).Returns([p1.Object, p2.Object]);
        var sut = new FcRemoteRestarter(source.Object);

        var result = sut.Restart();

        Assert.That(result.IsSuccess, Is.True);
        p1.Verify(p => p.Kill(), Times.Once);
        p2.Verify(p => p.Kill(), Times.Once);
    }

    [Test]
    public void Restart_ошибка_Kill_не_бросает()
    {
        var process = new Mock<IFcRemoteProcess>();
        process.Setup(p => p.Kill()).Throws(new InvalidOperationException("нет доступа"));
        var source = new Mock<IFcRemoteProcessSource>();
        source.Setup(s => s.ListByName("fc-remote")).Returns([process.Object]);
        var sut = new FcRemoteRestarter(source.Object);

        var result = sut.Restart();

        Assert.That(result.IsSuccess, Is.True);
    }
}
