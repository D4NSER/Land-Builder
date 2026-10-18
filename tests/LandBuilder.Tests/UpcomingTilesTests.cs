using LandBuilder.Domain;
using Xunit;

namespace LandBuilder.Tests;

public sealed class UpcomingTilesTests
{
    /// <summary>Whether some spot and rotation match every touching edge for <paramref name="tile"/>.</summary>
    private static bool HasCleanSpot(GameState state, TileSpec tile)
    {
        var probe = state.Clone();
        probe.CurrentTile = tile;
        return probe.CandidateSlots().Any(slot => Enumerable.Range(0, 6).Any(rotation =>
        {
            var eval = GameEngine.Evaluate(probe, slot, rotation);
            return eval.IsValid && eval.MismatchedEdges.Count == 0;
        }));
    }

    [Fact]
    public void TwoUpcomingTiles_AreShown_AndPlayedInOrder()
    {
        var state = GameState.CreateInitial(99);
        Assert.Equal(GameRules.UpcomingShown, state.Upcoming.Count);
        var queued = state.Upcoming.ToList();

        var move = GameEngine.FindBestPlacement(state)!;
        (state, _) = GameEngine.Place(state, move.Slot, move.Rotation);
        Assert.Equal(queued[0], state.CurrentTile);
        Assert.Equal(queued[1], state.Upcoming[0]);
        Assert.Equal(GameRules.UpcomingShown, state.Upcoming.Count);

        move = GameEngine.FindBestPlacement(state)!;
        (state, _) = GameEngine.Place(state, move.Slot, move.Rotation);
        Assert.Equal(queued[1], state.CurrentTile);
    }

    [Fact]
    public void EveryDealtTile_HasACleanSpot_WhenItIsDealt()
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var state = GameState.CreateInitial(seed * 104729);
            Assert.True(HasCleanSpot(state, state.CurrentTile!.Value), $"seed {seed}: opening tile");
            for (var turn = 0; turn < 40 && !state.IsGameOver; turn++)
            {
                var move = GameEngine.FindBestPlacement(state)!;
                (state, _) = GameEngine.Place(state, move.Slot, move.Rotation);
                if (state.Upcoming.Count > 0)
                    Assert.True(HasCleanSpot(state, state.Upcoming[^1]), $"seed {seed} turn {turn}: newly dealt tile fits nowhere cleanly");
            }
        }
    }

    [Fact]
    public void TheQueueEmptiesAsTheStackRunsOut()
    {
        var state = GameState.CreateInitial(5);
        state.StackCount = 1;
        state.Upcoming.RemoveRange(1, state.Upcoming.Count - 1);

        var move = GameEngine.FindBestPlacement(state)!;
        (state, _) = GameEngine.Place(state, move.Slot, move.Rotation);

        Assert.NotNull(state.CurrentTile);
        Assert.Empty(state.Upcoming);
        Assert.Equal(1, state.TilesLeft);
    }
}
