using Domain.AppState.Interfaces;
using Domain.Messages.Dto;
using Microsoft.Extensions.DependencyInjection;
using Shared.DI.Attributes;

namespace Application.AppState
{
    [AutoRegisterService(ServiceLifetime.Singleton)]
    public class AppStateService : IApplicationState
    {
        private bool _dbState {  get; set; } = false;
        private bool _needRessart {  get; set; } = false;
        private NewVersionResponse _newVersionInformation { get; set; } = new NewVersionResponse();

        public void DbStateUpdate(bool isOnLine) => _dbState = isOnLine;
        public bool DbState() => _dbState;

        public void UpdateNeedRestart(bool need) => _needRessart = need;
        public bool NeedRestart() => _needRessart;

        public void NewVersionInformationUpdate(NewVersionResponse value) => _newVersionInformation = value;
        public NewVersionResponse NewVersionInformation() => _newVersionInformation;
    }
}
