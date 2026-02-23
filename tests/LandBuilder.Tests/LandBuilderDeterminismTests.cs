using LandBuilder.Application;
using LandBuilder.Domain;
using Xunit;

namespace LandBuilder.Tests;

public class LandBuilderDeterminismTests
{
    [Fact]
    public void SameSeedAndCommands_ProduceIdenticalState()
    {
        var commands = new IGameCommand[]
        {
            new DrawTileCommand(),
            new PlaceTileCommand(0),
            new DrawTileCommand(),
            new PlaceTileCommand(1),
            new DrawTileCommand(),
            new PlaceTileCommand(3),
            new DrawTileCommand(),
            new PlaceTileCommand(4)
        };

        string RunOnce()
        {
            var session = new GameSession(GameState.CreateInitial(12345), new InMemoryEventSink());
            foreach (var command in commands)
                session.IssueCommand(command);

            var boardSig = string.Join(";", session.State.Board.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value.TileType}:{x.Value.RotationQuarterTurns}"));
            return $"{session.State.Coins}|{session.State.RngState}|{session.State.RngStep}|{session.State.CurrentTile}|{boardSig}";
        }

        var baseline = RunOnce();
        for (var i = 0; i < 5; i++)
            Assert.Equal(baseline, RunOnce());
    }
}
