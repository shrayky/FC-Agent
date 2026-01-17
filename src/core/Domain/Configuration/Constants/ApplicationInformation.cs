namespace Domain.Configuration.Constants
{
    public static class ApplicationInformation
    {
        public const string Name = "fc";
        public const string Manufacture = "Automation";
        public const string Description = "Служба агента для централизованной настройки баз данных фронтола";
        public const string ServiceName = "DS:FC Agent";
        
        public const int Version = 1;
        public const int Assembly = 12;

        public static object Information() => new { Name, Version, Assembly, Description};
    }
}
