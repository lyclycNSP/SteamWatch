namespace SteamWatch.Infrastructure.Startup;

public interface IStartupRegistry
{
    string? Read(string name);

    void Write(string name, string command);

    void Delete(string name);
}
