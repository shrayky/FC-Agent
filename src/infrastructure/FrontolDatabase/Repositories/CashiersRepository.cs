using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using CSharpFunctionalExtensions;
using Domain.Frontol.Interfaces;
using Domain.Frontol.Models.Cashiers;
using FrontolDatabase.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Repositories;

public class CashiersRepository : IFrontolCashiers
{
    private readonly ILogger<CashiersRepository> _logger;
    private readonly MainDbCtx _ctx;
    private readonly IFrontolMainDb _mainDb;

    public CashiersRepository(ILogger<CashiersRepository> logger, MainDbCtx ctx, IFrontolMainDb mainDb)
    {
        _logger = logger;
        _ctx = ctx;
        _mainDb = mainDb;
    }

    // Frontol: CODE = смещение + код из пакета. Пишем и удаляем только коды строго больше смещения.
    // Профиль ищем по PROFILE.CODE; нет профиля — ProfileID = 0, кассира всё равно пишем.
    // Кассир списка — CODE больше смещения. Нет его в пакете — строка USER удаляется:
    // флага Deleted Frontol не хватает, кассир остаётся в справочнике.
    // Пользователи с кодом не выше смещения не трогаем. В PSWRD пишется MD5 пароля.
    public async Task<Result> Apply(int offset, IReadOnlyList<CashierItem> cashiers)
    {
        try
        {
            if (_ctx.Users == null)
                return Result.Failure("Не удалось открыть USER");

            var bdoCode = await BdoCode();
            var rows = cashiers
                .Select(item => (Code: offset + item.Code, Item: item))
                .Where(row => row.Code > offset)
                .ToList();
            var profileIds = await ProfileIds(rows.Select(row => row.Item.ProfileCode).Distinct().ToList());

            foreach (var row in rows)
            {
                var profileId = profileIds.GetValueOrDefault(row.Item.ProfileCode);
                var existing = _ctx.Users.Local.FirstOrDefault(user => user.Code == row.Code)
                    ?? await _ctx.Users.FirstOrDefaultAsync(user => user.Code == row.Code);

                if (existing == null)
                {
                    var id = await _mainDb.NextChangeId();
                    _ctx.Users.Add(new User
                    {
                        Id = id,
                        Code = row.Code,
                        Name = row.Item.Name,
                        ProfileId = profileId,
                        Pswrd = PasswordHash(row.Item.Password),
                        Card = row.Item.Barcode,
                        Deleted = 0,
                        BdoCode = bdoCode,
                        ChangeCount = id
                    });
                    continue;
                }

                existing.Name = row.Item.Name;
                existing.ProfileId = profileId;
                existing.Pswrd = PasswordHash(row.Item.Password);
                existing.Card = row.Item.Barcode;
                existing.ChangeCount = await _mainDb.NextChangeId();
                existing.Deleted = 0;
            }

            var keep = rows.Select(row => row.Code).ToHashSet();
            var managed = await _ctx.Users.Where(user => user.Code > offset).ToListAsync();
            foreach (var user in managed)
            {
                if (keep.Contains(user.Code))
                    continue;

                _ctx.Users.Remove(user);
            }

            await _ctx.SaveChangesAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка записи кассиров в Frontol");
            return Result.Failure(ex.Message);
        }
    }

    // Frontol: MD5 в нижнем регистре, кодировка WIN1251. Пустая строка — d41d8cd98f00b204e9800998ecf8427e.
    private static string PasswordHash(string? password)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1251).GetBytes(password ?? string.Empty);
        return Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
    }

    private async Task<int> BdoCode()
    {
        if (_ctx.CustomDb == null)
            return 0;

        var row = await _ctx.CustomDb.AsNoTracking().FirstOrDefaultAsync();
        return row?.Code ?? 0;
    }

    private async Task<Dictionary<int, int>> ProfileIds(IReadOnlyList<int> codes)
    {
        if (_ctx.UserProfiles == null || codes.Count == 0)
            return [];

        var rows = await FirebirdQuery.Load(codes, FirebirdQuery.KeyBatch, batch =>
        {
            var parameter = Expression.Parameter(typeof(Profile), "profile");
            var property = Expression.Property(parameter, nameof(Profile.Code));
            return _ctx.UserProfiles!.Where(FirebirdQuery.OrEquals<Profile, int>(parameter, property, batch));
        });
        return rows
            .GroupBy(profile => profile.Code)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

}
