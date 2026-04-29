using SteamWatch.Core.Steam;

namespace SteamWatch.Infrastructure.Steam;

public interface IProcessSnapshotProvider
{
    IReadOnlyList<SteamProcessSnapshot> GetProcesses();
}
