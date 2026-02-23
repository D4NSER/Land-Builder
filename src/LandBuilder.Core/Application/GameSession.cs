using LandBuilder.Domain;
using LandBuilder.Infrastructure;

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
    private readonly HighScoreRepository? _highScoreRepository;
    private readonly string? _highScorePath;

    public GameState State { get; private set; }

    public GameSession(GameState initialState, IEventSink sink)
    {
        State = initialState;
        _sink = sink;
    }

    public GameSession(GameState initialState, IEventSink sink, HighScoreRepository highScoreRepository, string highScorePath)
        : this(initialState, sink)
    {
        _highScoreRepository = highScoreRepository;
        _highScorePath = highScorePath;
    }

    public void IssueCommand(IGameCommand command)
    {
        var (next, events) = _simulator.Apply(State, command);
        State = next;

        foreach (var ev in events)
        {
            _sink.Publish(ev);
            HandleHighScorePersistence(ev);
        }
    }

    private void HandleHighScorePersistence(IDomainEvent domainEvent)
    {
        if (domainEvent is not ScoreSubmittedEvent submitted ||
            _highScoreRepository is null ||
            string.IsNullOrWhiteSpace(_highScorePath))
        {
            return;
        }

        var currentHighScore = _highScoreRepository.LoadHighScore(_highScorePath);
        if (submitted.Score <= currentHighScore)
            return;

        _highScoreRepository.SaveHighScore(_highScorePath, submitted.Score);
        _sink.Publish(new HighScoreUpdatedEvent(currentHighScore, submitted.Score));
    }
}
