namespace Domain.Frontol.Models.Settings;

public record UserInterface
{
    // null — секции нет, таблицу не меняем. Пустой список тоже ничего не удаляет.
    public List<TouchLayout>? Layout { get; set; }
}
