using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace FrontolDatabase;

public static class FirebirdQuery
{
    // Текст запроса и BLR ограничены 64 КБ. 200 сравнений укладываются с запасом.
    public const int KeyBatch = 200;

    public static IEnumerable<IReadOnlyList<T>> Batches<T>(IReadOnlyList<T> values, int size)
    {
        for (var offset = 0; offset < values.Count; offset += size)
        {
            var count = Math.Min(size, values.Count - offset);
            var batch = new T[count];
            for (var i = 0; i < count; i++)
                batch[i] = values[offset + i];
            yield return batch;
        }
    }

    public static async Task<List<T>> Load<T, TKey>(
        IReadOnlyList<TKey> keys,
        int batchSize,
        Func<IReadOnlyList<TKey>, IQueryable<T>> query)
    {
        var rows = new List<T>();
        foreach (var batch in Batches(keys, batchSize))
            rows.AddRange(await query(batch).ToListAsync());
        return rows;
    }

    // Firebird 2.1: Contains по локальному списку даёт WHERE FALSE, булева типа нет.
    public static Expression<Func<T, bool>> OrEquals<T, TKey>(
        ParameterExpression parameter,
        MemberExpression property,
        IReadOnlyList<TKey> values)
    {
        Expression body = Expression.Equal(property, Expression.Constant(values[0]));
        for (var i = 1; i < values.Count; i++)
            body = Expression.OrElse(body, Expression.Equal(property, Expression.Constant(values[i])));

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}
