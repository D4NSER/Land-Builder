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

    public void Advance(double deltaSeconds)
    {
        _accumulator += deltaSeconds;

        while (_accumulator >= _tickDurationSeconds)
        {
            _session.Dispatch(new TickCommand(1));
            _accumulator -= _tickDurationSeconds;
        }
    }
}
