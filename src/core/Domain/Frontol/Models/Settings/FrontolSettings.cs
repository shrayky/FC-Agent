namespace Domain.Frontol.Models.Settings;

public record FrontolSettings
{
    public GlobalControl GlobalControl { get; set; } = new();

    public List<UserProfile> UserProfiles { get; init; } = [];

    public FrontolAgentScripts Scripts { get; set; } = new();

    public List<FrontolParameter> Settings { get; set; } = [];

    // null — секции нет в пакете, таблицу не меняем. Пустой список удаляет из таблицы строки, которых нет в пакете.
    public List<DocumentKind>? DocumentKinds { get; set; }

    public List<DocumentTemplate>? DocumentTemplates { get; set; }

    public List<DocumentPrintGroup>? PrintGroups { get; set; }

    public List<DocumentComment>? Comments { get; set; }

    // это поле означет что задание по выгрузек настроек из фронтола в центр завершено
    public bool Updated { get; set; }
}