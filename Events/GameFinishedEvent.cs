namespace Events;

public class GameFinishedEvent
{
    public Guid GameId { get; set; }
    public string? WinnerId { get; set; }
    public Dictionary<string, Move> Moves { get; set; } = new Dictionary<string, Move>();
    public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

}