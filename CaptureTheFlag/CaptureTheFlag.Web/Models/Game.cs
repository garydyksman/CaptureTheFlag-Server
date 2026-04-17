using CaptureTheFlag.Web.Enums;

namespace CaptureTheFlag.Web.Models;

public class Game
{
    public int Id { get; set; }
    public DateTime CreateTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public GameStatus Status { get; set; }

    public ICollection<GameDevice> Devices { get; set; } = new List<GameDevice>();
}