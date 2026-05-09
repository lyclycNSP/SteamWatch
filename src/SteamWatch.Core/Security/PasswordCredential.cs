namespace SteamWatch.Core.Security;

public sealed record PasswordCredential(
    string SaltBase64,
    string HashBase64,
    int Iterations,
    int KeySize);
