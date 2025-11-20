using Domain.Messages.Dto;

namespace Domain.AppState.Interfaces
{
    public interface IApplicationState
    {
        void DbStateUpdate(bool isOnline);
        bool DbState();
        
        void UpdateNeedRestart(bool need);
        bool NeedRestart();

        void NewVersionInformationUpdate(NewVersionResponse value);
        NewVersionResponse NewVersionInformation();
    }
}
