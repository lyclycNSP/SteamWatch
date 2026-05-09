using System.Security.Cryptography;
using System.Text;

namespace SteamWatch.Core.Security;

public static class PasswordHasher
{
    public const int DefaultIterations = 100_000;
    public const int DefaultKeySize = 32;
    private const int SaltSize = 16;

    public static PasswordCredential Create(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Hash(password, salt, DefaultIterations, DefaultKeySize);

        return new PasswordCredential(
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash),
            DefaultIterations,
            DefaultKeySize);
    }

    public static bool Verify(string password, PasswordCredential? credential)
    {
        if (string.IsNullOrEmpty(password) || credential is null)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(credential.SaltBase64);
            var expectedHash = Convert.FromBase64String(credential.HashBase64);
            var actualHash = Hash(password, salt, credential.Iterations, credential.KeySize);

            return expectedHash.Length == actualHash.Length
                && CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static byte[] Hash(string password, byte[] salt, int iterations, int keySize)
    {
        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations));
        }

        if (keySize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keySize));
        }

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            keySize);
    }
}
