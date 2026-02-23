using LandBuilder.Domain;

namespace LandBuilder.Application;

public sealed class DeterministicTickScheduler
{
    private readonly GameSession _session;
    private readonly double _tickDurationSeconds;
    private double _accumulator;

    public DeterministicTickScheduler(GameSession session, int ticksPerSecond)
    {
        _session = session;
        _tickDurationSeconds = 1.0 / ticksPerSecond;
    }

    public int Advance(double deltaSeconds)
    {
        _accumulator += deltaSeconds;
        var processedTicks = 0;

        while (_accumulator >= _tickDurationSeconds)
        {
            if (_session.State.CurrentTile is null)
            {
                _session.IssueCommand(new DrawTileCommand());
            }
            _accumulator -= _tickDurationSeconds;
            processedTicks++;
        }

        return processedTicks;
    }
}
