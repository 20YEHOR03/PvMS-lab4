using Lab4Bot.Models;

namespace Lab4Bot;

using Microsoft.EntityFrameworkCore;

public class UniversityDbContext : DbContext
{
    public DbSet<Building> Buildings { get; set; }
    public DbSet<Floor> Floors { get; set; }
    public DbSet<Classroom> Classrooms { get; set; }
    public DbSet<UserActivity> UserActivities { get; set; }

    private string _connectionString;

    public UniversityDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(_connectionString);
    }
    
    public void SeedData()
    {
        if (!Buildings.Any())
        {
            Buildings.Add(new Building { Name = "Головний корпус" });
            Buildings.Add(new Building { Name = "Корпус З" });
            Buildings.Add(new Building { Name = "Корпус І" });
            SaveChanges();
        }

        if (!Floors.Any())
        {
            var buildings = Buildings.ToList();
            foreach (var building in buildings)
            {
                for (int i = 0; i < 5; i++)
                {
                    Floors.Add(new Floor { BuildingId = building.Id, Number = i + 1 });
                }
                
            }
            SaveChanges();
        }

        if (!Classrooms.Any())
        {
            var floors = Floors.ToList();
            foreach (var floor in floors)
            {
                string building = floor.Building.Name.Contains('З') ? "з" :
                    floor.Building.Name.Contains('І') ? "і" : "";
                for (int i = 1; i <= 20; i++)
                {
                    Classrooms.Add(new Classroom { FloorId = floor.Id, Number = $"{floor.Number}" +
                        $"{(i < 10 ? "0" + i : i)}{building}"});
                }
            }
            SaveChanges();
        }
    }
}








