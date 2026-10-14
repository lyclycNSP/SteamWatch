using SteamWatch.App;
using SteamWatch.Core.Steam;

namespace SteamWatch.Tests;

[TestClass]
public sealed class GameLibraryViewModelTests
{
    [TestMethod]
    public void SearchRunningRulesAndCategoryCompose()
    {
        var state = new GameLibraryViewModel { SearchText = "730", RunningOnly = true, RulesOnly = true, Category = SteamAppCategory.Game };
        Assert.IsTrue(state.Matches(730, "Fixture", true, SteamAppCategory.Game, true));
        Assert.IsFalse(state.Matches(731, "Fixture", true, SteamAppCategory.Game, true));
        Assert.IsFalse(state.Matches(730, "Fixture", false, SteamAppCategory.Game, true));
        Assert.IsFalse(state.Matches(730, "Fixture", true, SteamAppCategory.Game, false));
        Assert.IsFalse(state.Matches(730, "Fixture", true, SteamAppCategory.Tool, true));
    }

    [TestMethod]
    public void UnknownAndToolsRemainVisibleWithoutCategoryFilter()
    {
        var state = new GameLibraryViewModel();
        Assert.IsTrue(state.Matches(730, "Unknown", false, SteamAppCategory.Unknown, false));
        Assert.IsTrue(state.Matches(731, "Tool", false, SteamAppCategory.Tool, false));
        state.Category = SteamAppCategory.Unknown;
        Assert.IsTrue(state.Matches(730, "Unknown", false, SteamAppCategory.Unknown, false));
        Assert.IsFalse(state.Matches(731, "Tool", false, SteamAppCategory.Tool, false));
    }

    [TestMethod]
    public void SearchIgnoresOuterWhitespaceAndNameCase()
    {
        var state = new GameLibraryViewModel { SearchText = "  aRk  " };
        Assert.IsTrue(state.Matches(1188930, "Chrono Ark", false, SteamAppCategory.Unknown, true));
        state.SearchText = "8893";
        Assert.IsTrue(state.Matches(1188930, "Chrono Ark", false, SteamAppCategory.Unknown, true));
    }

    [TestMethod]
    public void SelectionSurvivesReorderingByIdentity()
    {
        var state = new GameLibraryViewModel();
        state.SelectGame(730);
        Assert.AreEqual(730, state.ReconcileSelection([731, 730, 732]));
        Assert.AreEqual(730, state.ReconcileSelection([732, 731, 730]));
    }

    [TestMethod]
    public void HiddenSelectionClearsWithoutSelectingAnUnrelatedRow()
    {
        var state = new GameLibraryViewModel();
        state.SelectGame(730);
        Assert.IsNull(state.ReconcileSelection([731]));
        Assert.IsNull(state.ReconcileSelection([730, 731]));
    }

    [TestMethod]
    public void ReconcilingRefreshDoesNotResetFilters()
    {
        var state = new GameLibraryViewModel { SearchText = "fixture", RunningOnly = true, RulesOnly = true, Category = SteamAppCategory.Tool };
        state.SelectGame(730);
        state.ReconcileSelection([730]);
        Assert.IsTrue(state.Matches(730, "Fixture", true, SteamAppCategory.Tool, true));
        Assert.IsFalse(state.Matches(730, "Fixture", false, SteamAppCategory.Tool, true));
        Assert.AreEqual("fixture", state.SearchText);
    }
}
