using LandBuilder.Domain;
using Xunit;
using Xunit.Abstractions;

namespace LandBuilder.Tests;

/// <summary>
/// Plays full runs with the greedy hint bot and checks the pacing stays in a sensible band.
/// A good player should beat the bot; a careless one should still get a satisfying run.
/// </summary>
public sealed class BalanceTests
{
    private readonly ITestOutputHelper _output;

    public BalanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void GreedyBotRuns_LandInTargetPacingBand()
    {
        var lengths = new List<int>();
        var scores = new List<long>();
        var quests = new List<int>();
        var milestones = new List<int>();

        for (ulong seed = 1; seed <= 16; seed++)
        {
            var state = GameState.CreateInitial(seed * 7919);
            var guard = 0;
            while (!state.IsGameOver && guard++ < 2000)
            {
                var best = GameEngine.FindBestPlacement(state)!;
                (state, _) = GameEngine.Place(state, best.Slot, best.Rotation);
            }

            Assert.True(state.IsGameOver, $"Seed {seed} never ended.");
            lengths.Add(state.Stats.TilesPlaced);
            scores.Add(state.Score);
            quests.Add(state.Stats.QuestsCompleted);
            milestones.Add(state.MilestonesReached);
            _output.WriteLine($"seed {seed,2} {state.Theme,-9}: tiles {state.Stats.TilesPlaced,4} score {state.Score,6} quests {state.Stats.QuestsCompleted,3}/{state.Stats.QuestsFailed,-3} perfect {state.Stats.PerfectPlacements,3} flawless {state.Stats.FlawlessPlacements,2} milestones {state.MilestonesReached,2} rungs {state.Stats.TiersReached,3} closed {state.Stats.RegionsClosed,3} town {state.Stats.BestVillage} bestStreak {state.Stats.BestStreak,3} unlocked {string.Join(",", state.Unlocked.Skip(5))}");
        }

        var avgLength = lengths.Average();
        _output.WriteLine($"avg tiles {avgLength:F1}, avg score {scores.Average():F0}, avg quests {quests.Average():F1}, avg milestones {milestones.Average():F1}");

        // Runs should be meaty but finite: roughly 15-45 minutes of play for a person.
        Assert.InRange(avgLength, 70, 260);
        Assert.InRange(lengths.Max(), 0, 600);
        Assert.True(quests.Average() >= 3, "Quests should complete regularly.");
        Assert.True(milestones.Average() >= 2, "Players should see the unlock milestones.");
    }
}
