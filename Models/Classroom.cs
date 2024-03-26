namespace Lab4Bot.Models;

public class Classroom
{
    public int Id { get; set; }
    public int FloorId { get; set; }
    public string Number { get; set; }
    
    public Floor Floor { get; set; }
}