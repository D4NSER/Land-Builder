using LandBuilder.Domain;

namespace LandBuilder.Application;

public interface IEventSink
{
    void Publish(IReadOnlyList<IDomainEvent> events);
}

public sealed class InMemoryEventSink : IEventSink
{
    private readonly List<IDomainEvent> _events = new();

    public IReadOnlyList<IDomainEvent> Events => _events;

    public void Publish(IReadOnlyList<IDomainEvent> events)
    {
        _events.AddRange(events);
    }

    public void Clear() => _events.Clear();
}

public sealed class GameSession
{
    private readonly IEventSink _eventSink;

    public GameState State { get; private set; }

    public GameSession(GameState initialState, IEventSink eventSink)
    {
        State = initialState;
        _eventSink = eventSink;
    }

    public void Dispatch(IGameCommand command)
    {
        var result = DeterministicSimulator.Apply(State, command);
        State = result.State;
        _eventSink.Publish(result.Events);
    }
}
