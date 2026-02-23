using LandBuilder.Domain;

namespace LandBuilder.Application;

public interface IEventSink
{
    void Publish(IDomainEvent domainEvent);
}

public sealed class InMemoryEventSink : IEventSink
{
    private readonly List<IDomainEvent> _events = new();
    public IReadOnlyList<IDomainEvent> Events => _events;
    public void Publish(IDomainEvent domainEvent) => _events.Add(domainEvent);
    public void Clear() => _events.Clear();
}

public sealed class GameSession
{
    private readonly DeterministicSimulator _simulator = new();
    private readonly IEventSink _sink;

    public GameState State { get; private set; }

    public GameSession(GameState initialState, IEventSink sink)
    {
        State = initialState;
        _sink = sink;
    }

    public void IssueCommand(IGameCommand command)
    {
        var (next, events) = _simulator.Apply(State, command);
        State = next;
        foreach (var ev in events)
            _sink.Publish(ev);
    }
}
