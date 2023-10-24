namespace Events;

public class GameStartedEvent
{
    public Guid GameId { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

}