using SteamWatch.Infrastructure.Steam;

namespace SteamWatch.Tests;

[TestClass]
public sealed class CommandLineSplitterTests
{
    [TestMethod]
    public void Split_QuotedExecutablePath_KeepsPathAsSingleArgument()
    {
        var parts = CommandLineSplitter.Split("\"C:\\Program Files\\Steam\\steam.exe\" -silent");

        Assert.HasCount(2, parts);
        Assert.AreEqual("C:\\Program Files\\Steam\\steam.exe", parts[0]);
        Assert.AreEqual("-silent", parts[1]);
    }

    [TestMethod]
    public void Split_SteamAppArgument_ReturnsSeparateArgument()
    {
        var parts = CommandLineSplitter.Split("\"C:\\Game\\game.exe\" steam_app_730 -fullscreen");

        CollectionAssert.Contains(parts.ToArray(), "steam_app_730");
    }

    [TestMethod]
    public void Split_EmptyInput_ReturnsEmpty()
    {
        var parts = CommandLineSplitter.Split("");

        Assert.IsEmpty(parts);
    }
}
