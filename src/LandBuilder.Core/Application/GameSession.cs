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

/// <summary>
/// Owns the current run: applies placements through <see cref="GameEngine"/>, keeps history for undo,
/// and forwards events to the presentation.
/// </summary>
public sealed class GameSession
{
    private readonly Stack<GameState> _history = new();
    private readonly IEventSink _sink;

    public GameState State { get; private set; }

    public GameSession(GameState initialState, IEventSink sink)
    {
        State = initialState;
        _sink = sink;
    }

    public static GameSession StartNew(ulong seed, IEventSink sink) => new(GameState.CreateInitial(seed), sink);

    public bool CanUndo => _history.Count > 0 && State.UndosRemaining > 0 && !State.IsGameOver;

    public IReadOnlyList<IDomainEvent> Place(int slot, int rotation)
    {
        var (next, events) = GameEngine.Place(State, slot, rotation);
        if (!ReferenceEquals(next, State))
        {
            _history.Push(State);
            State = next;
        }

        foreach (var ev in events)
            _sink.Publish(ev);

        return events;
    }

    public IReadOnlyList<IDomainEvent> PlaceWonder(int cardIndex, int slot, int rotation)
    {
        var (next, events) = GameEngine.PlaceWonder(State, cardIndex, slot, rotation);
        if (!ReferenceEquals(next, State))
        {
            _history.Push(State);
            State = next;
        }

        foreach (var ev in events)
            _sink.Publish(ev);

        return events;
    }

    /// <summary>Restores the state before the last placement. Undos are a limited per-run resource.</summary>
    public bool TryUndo(out int undoneSlot)
    {
        undoneSlot = 0;
        if (!CanUndo)
            return false;

        var current = State;
        var previous = _history.Pop().Clone();
        previous.UndosRemaining = current.UndosRemaining - 1;
        undoneSlot = current.Board.Keys.Except(previous.Board.Keys).FirstOrDefault();
        State = previous;
        return true;
    }
}
