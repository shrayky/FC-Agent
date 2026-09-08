using System.Diagnostics;
using Domain.Agent.Interfaces;

namespace CentralServerExchange.Services;

public class WindowsFcRemoteProcessSource : IFcRemoteProcessSource
{
    public IReadOnlyList<IFcRemoteProcess> ListByName(string processName)
    {
        return Process.GetProcessesByName(processName)
            .Select(process => (IFcRemoteProcess)new ProcessAdapter(process))
            .ToList();
    }

    private sealed class ProcessAdapter(Process process) : IFcRemoteProcess
    {
        public int Id => process.Id;

        public void Kill()
        {
            try
            {
                process.Kill();
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
