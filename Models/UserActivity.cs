namespace Lab4Bot.Models;

public class UserActivity
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public int MessagesSent { get; set; }
    public string LastClassroomSearched { get; set; }
    public DateTime Time { get; set; }
}