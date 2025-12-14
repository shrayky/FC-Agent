using System.Text.Json;
using FrontolDatabase.Entitys;
using Microsoft.Extensions.Logging;

namespace FrontolDatabase.Services;

public class UserProfileDefaultSecurityService
{
    private readonly ILogger<UserProfileDefaultSecurityService> _logger;

    private const string SecurityJsonFileName = "user-profile-security.json";
    
    public UserProfileDefaultSecurityService(ILogger<UserProfileDefaultSecurityService> logger)
    {
        _logger = logger;
    }

    public async Task<HashSet<int>> GetAllSecurityCodesAsync()
    {
        try
        {
            var jsonPath = GetJsonFilePath();
            
            if (!File.Exists(jsonPath))
            {
                _logger.LogWarning("Файл {FileName} не найден по пути: {Path}", SecurityJsonFileName, jsonPath);
                return new HashSet<int>();
            }

            var jsonContent = await File.ReadAllTextAsync(jsonPath);
            var jsonDoc = JsonDocument.Parse(jsonContent);
            
            var codes = new HashSet<int>();
            ExtractSecurityCodes(jsonDoc.RootElement, codes);
            
            _logger.LogDebug("Загружено {Count} кодов Securities из JSON", codes.Count);
            
            return codes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке кодов Securities из JSON");
            return new HashSet<int>();
        }
    }
    
    public async Task<List<Security>> CreateDefaultSecuritiesAsync(int profileId, HashSet<int> enabledCodes)
    {
        var allCodes = await GetAllSecurityCodesAsync();
        var securities = new List<Security>();
        
        foreach (var securityCode in allCodes)
        {
            var value = enabledCodes.Contains(securityCode) ? 1 : 0;
            
            securities.Add(new Security
            {
                ProfileId = profileId,
                SecurityCode = securityCode,
                Value = value
            });
        }
        
        return securities;
    }

    private string GetJsonFilePath()
        => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SecurityJsonFileName);
    
    private void ExtractSecurityCodes(JsonElement element, HashSet<int> codes)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object)
                        {
                            if (item.TryGetProperty("code", out var codeElement) && 
                                codeElement.ValueKind == JsonValueKind.Number)
                            {
                                codes.Add(codeElement.GetInt32());
                            }
                            else
                            {
                                ExtractSecurityCodes(item, codes);
                            }
                        }
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    ExtractSecurityCodes(property.Value, codes);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ExtractSecurityCodes(item, codes);
            }
        }
    }
}