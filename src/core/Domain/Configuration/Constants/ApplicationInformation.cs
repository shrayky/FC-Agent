namespace Domain.Configuration.Constants
{
    public static class ApplicationInformation
    {
        public const string Name = "FrontolConfiguratorAgent";
        public const string Manufacture = "Automation";
        public const string Description = "Служба для централизованной настройки баз данных фронтола";
        public const string ServiceName = "DS:Frontol configurator agent";
        
        public const int Version = 1;
        public const int Assembly = 1;

        public static object Information() => new { Name, Version, Assembly, Description};
    }
}
