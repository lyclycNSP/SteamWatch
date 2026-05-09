using SteamWatch.Core.Security;

namespace SteamWatch.Tests;

[TestClass]
public sealed class PasswordHasherTests
{
    [TestMethod]
    public void Verify_MatchingPassword_ReturnsTrue()
    {
        var credential = PasswordHasher.Create("1234");

        Assert.IsTrue(PasswordHasher.Verify("1234", credential));
    }

    [TestMethod]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var credential = PasswordHasher.Create("1234");

        Assert.IsFalse(PasswordHasher.Verify("4321", credential));
    }

    [TestMethod]
    public void Create_EmptyPassword_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => PasswordHasher.Create(""));
    }
}
