using Domain.Entitys.Logs.Dto;

namespace Domain.Entitys.Logs.Interfaces
{
    public interface ILogCollectorService
    {
        Task<LogPacket> Collect();
        Task<LogPacket> Collect(string selectedFileName);
    }
}
