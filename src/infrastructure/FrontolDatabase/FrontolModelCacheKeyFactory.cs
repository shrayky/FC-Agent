using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FrontolDatabase;

/// <summary>
/// Модель <see cref="MainDbCtx"/> зависит от фактической схемы базы, поэтому её отпечаток
/// входит в ключ кеша модели: иначе для базы другой версии Frontol EF переиспользовал бы
/// уже построенную (и уже не подходящую) модель.
/// </summary>
internal sealed class FrontolModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => (context.GetType(), (context as MainDbCtx)?.Schema.Fingerprint, designTime);
}
