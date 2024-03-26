namespace Lab4Bot.Models;

public class Floor
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public int Number { get; set; }
    public Building Building { get; set; }
}