using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Settings;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class TouchLayoutRepository : IFrontolTouchLayouts
{
    private const int NameLimit = 100;

    private readonly ILogger<TouchLayoutRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly IFrontolMainDb _mainDb;

    public TouchLayoutRepository(
        ILogger<TouchLayoutRepository> logger,
        MainDbCtx ctx,
        IFrontolMainDb mainDb)
    {
        _logger = logger;
        _ctx = ctx;
        _mainDb = mainDb;
    }

    public async Task<Result<UserInterface>> Read()
    {
        if (_ctx.TouchLayouts == null)
            return Result.Success(new UserInterface());

        try
        {
            var rows = await _ctx.TouchLayouts.AsNoTracking().ToListAsync();
            return Result.Success(new UserInterface
            {
                Layout = rows.Select(row => new TouchLayout
                {
                    Code = row.Code,
                    Name = row.Name ?? string.Empty,
                    Data = row.Data ?? []
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка чтения макетов TouchLayt");
            return Result.Failure<UserInterface>(ex.Message);
        }
    }

    // Пустой список и null таблицу не меняют: строки только добавляются и обновляются по Code.
    public async Task<Result> Apply(IReadOnlyList<TouchLayout>? layouts)
    {
        if (layouts == null || layouts.Count == 0)
            return Result.Success();

        if (_ctx.TouchLayouts == null)
            return Result.Failure("Не удалось открыть TOUCHLAYT");

        var tooLong = layouts.FirstOrDefault(item => (item.Name ?? string.Empty).Length > NameLimit);
        if (tooLong != null)
            return Result.Failure($"Макет {tooLong.Code}: имя длиннее {NameLimit} символов");

        try
        {
            var incoming = layouts
                .GroupBy(item => item.Code)
                .Select(group => group.Last())
                .ToList();
            var existing = await _ctx.TouchLayouts.ToListAsync();
            var byCode = existing
                .GroupBy(row => row.Code)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var item in incoming)
            {
                if (!byCode.TryGetValue(item.Code, out var row))
                {
                    row = new TouchLayt
                    {
                        Id = await _mainDb.NextChangeId(),
                        Code = item.Code
                    };
                    _ctx.TouchLayouts.Add(row);
                    byCode[item.Code] = row;
                }

                row.Name = item.Name ?? string.Empty;
                row.Data = item.Data ?? [];
            }

            await _ctx.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка записи макетов TouchLayt");
            return Result.Failure(ex.Message);
        }
    }
}
