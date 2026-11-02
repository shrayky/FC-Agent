using Domain.Frontol.Interfaces;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace FrontolDatabase;

[AutoRegisterService(ServiceLifetime.Singleton)]
public sealed class FrontolConnectionRelease : IFrontolConnectionRelease
{
    public void Release() => FbConnection.ClearAllPools();
}
