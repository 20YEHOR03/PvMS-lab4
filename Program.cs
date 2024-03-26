using Lab4Bot;
using Lab4Bot.Models;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using User = Telegram.Bot.Types.User;


class Program
{
    private static ITelegramBotClient _botClient;
    private static UniversityDbContext _dbContext = new UniversityDbContext();

    static async Task Main()
    {
        _botClient = new TelegramBotClient(""); //TODO: use your token

         var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message },
            ThrowPendingUpdates = true,
        };
         
        using (var dbContext = new UniversityDbContext())
        {
            dbContext.Database.EnsureCreated();
            dbContext.SeedData();
        }

        using var cts = new CancellationTokenSource();

        _botClient.StartReceiving(HandleUpdate, HandleError, receiverOptions, cts.Token);

        var me = await _botClient.GetMeAsync();
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"{me.FirstName} запущений!");

        await Task.Delay(-1);
    }

    private static async Task HandleError(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
    {
        var errorMessage = error switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => error.ToString()
        };

        Console.WriteLine(errorMessage);
    }

    private static async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageUpdate(botClient, update.Message);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
    }

    private static async Task HandleMessageUpdate(ITelegramBotClient botClient, Message message)
    {
        var user = message.From;
        Console.WriteLine($"{user.FirstName} ({user.Id}) написав повідомлення: {message.Text}");

        var chatId = message.Chat.Id;
        switch (message.Type)
        {
            case MessageType.Text:
                await HandleTextMessage(botClient, chatId, message.Text, message.MessageId, user);
                break;
            default:
                await botClient.SendTextMessageAsync(chatId, "Використовуйте лише текст!");
                break;
        }
    }
    
    private static async Task HandleTextMessage(ITelegramBotClient botClient, long chatId, string text, int messageId, User user)
    {
        if (text == "/start")
        {
            await ShowStartMenu(botClient, chatId);
        }
        else if (text == "Пошук аудиторії")
        {
            await botClient.SendTextMessageAsync(chatId, "Введіть номер аудиторії (номер поверху та літера корпусу, наприклад, 302і):");
        }
        else if (text == "Статистика")
        {
            await ShowStatisticsMenu(botClient, chatId);
        }
        else if (text == "Інформація про корпуси")
        {
            await ShowBuildingsMenu(botClient, chatId);
        }
        else if (text is "Повернутися" or "Кількість підписників" or "Власна активність")
        {
            await HandleStatisticsMenu(botClient, chatId, text, messageId, user);
        }
        else
        {
            if (IsBuilding(text))
            {
                await HandleBuildingSelection(botClient, chatId, text);
            }
            else if (text.Length > 1 && char.IsDigit(text[0]))
            {
                await HandleRoomSearch(botClient, chatId, text, user);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Введено невірний формат аудиторії. Будь ласка, введіть номер аудиторії у форматі 302і.");
            }
        }
    }

    private static async Task ShowStartMenu(ITelegramBotClient botClient, long chatId)
    {
        var replyMarkup = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton("Пошук аудиторії"),
            new KeyboardButton("Статистика"),
            new KeyboardButton("Інформація про корпуси"),
        })
        {
            ResizeKeyboard = true
        };

        await botClient.SendTextMessageAsync(chatId, "Вітаємо! Оберіть дію:", replyMarkup: replyMarkup);
    }

    private static async Task HandleRoomSearch(ITelegramBotClient botClient, long chatId, string roomNumber, User user)
    {
        using (var dbContext = new UniversityDbContext())
        {
            var room = dbContext.Classrooms.FirstOrDefault(r => r.Number == roomNumber);
            if (room != null)
            {
                var floor = dbContext.Floors.Include(f => f.Building).FirstOrDefault(f => f.Id == room.FloorId);
                await botClient.SendTextMessageAsync(chatId, $"Аудиторія {roomNumber} знаходиться у корпусі {floor.Building.Name} на {floor.Number} поверсі.");
                
                dbContext.UserActivities.Add(new UserActivity
                {
                    UserId = user.Id,
                    MessagesSent = 1,
                    LastClassroomSearched = roomNumber,
                    Time = DateTime.UtcNow
                });
                dbContext.SaveChanges();
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, $"Аудиторію {roomNumber} не знайдено.");
            }
        }
    }
    
    private static async Task ShowStatisticsMenu(ITelegramBotClient botClient, long chatId)
    {
        var replyMarkup = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton("Повернутися"),
            new KeyboardButton("Кількість підписників"),
            new KeyboardButton("Власна активність"),
        })
        {
            ResizeKeyboard = true
        };

        await botClient.SendTextMessageAsync(chatId, "Статистика:", replyMarkup: replyMarkup);
    }
    
    private static async Task HandleStatisticsMenu(ITelegramBotClient botClient, long chatId, string text, int messageId, User user)
    {
        switch (text)
        {
            case "Повернутися":
                await ShowStartMenu(botClient, chatId);
                break;
            case "Кількість підписників":
                // Логіка для показу кількості підписників
                int subscriberCount = await GetSubscriberCount(botClient);
                await botClient.SendTextMessageAsync(chatId, $"Кількість підписників: {subscriberCount}");
                break;
            case "Власна активність":
                // Логіка для показу власної активності користувача
                await ShowUserActivity(botClient, chatId, user.Id);
                break;
            default:
                await botClient.SendTextMessageAsync(chatId, "Невідома команда");
                break;
        }
    }
    
    private static async Task<int> GetSubscriberCount(ITelegramBotClient botClient)
    {
        var uniqueUserIds = _dbContext.UserActivities.Select(ua => ua.UserId).Distinct().Count();
        return uniqueUserIds;
    }
    
    private static async Task ShowUserActivity(ITelegramBotClient botClient, long chatId, long userId)
    {
        using (var dbContext = new UniversityDbContext())
        {
            var userActivity = dbContext.UserActivities.Where(ua => ua.UserId == userId).OrderByDescending(ua => ua.Time).FirstOrDefault();
            var userActivityCount = dbContext.UserActivities.Count(ua => ua.UserId == userId);

            if (userActivity != null)
            {
                await botClient.SendTextMessageAsync(chatId, $"Ваша остання активність:" +
                                                             $"\nПовідомлень відправлено: {userActivity.MessagesSent}" +
                                                             $"\nОстання аудиторія, яку ви шукали: {userActivity.LastClassroomSearched}" +
                                                             $"\nЧас останньої активності: {userActivity.Time}" +
                                                             $"\nЗагальна кількість запитів: { userActivityCount}");
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Немає інформації про вашу активність.");
            }
        }
    }

    private static async Task ShowBuildingsMenu(ITelegramBotClient botClient, long chatId)
    {
        using (var dbContext = new UniversityDbContext())
        {
            var buildings = dbContext.Buildings.ToList();
            var keyboardButtons = buildings.Select(b => new KeyboardButton(b.Name));
            var replyMarkup = new ReplyKeyboardMarkup(keyboardButtons.Append(new KeyboardButton("Повернутися")).ToArray());

            await botClient.SendTextMessageAsync(chatId, "Оберіть корпус:", replyMarkup: replyMarkup);
        }
    }
    
    private static bool IsBuilding(string text)
    {
        using (var dbContext = new UniversityDbContext())
        {
            return dbContext.Buildings.Any(b => b.Name == text);
        }
    }

    private static async Task HandleBuildingSelection(ITelegramBotClient botClient, long chatId, string buildingName)
    {
        using (var dbContext = new UniversityDbContext())
        {
            var building = dbContext.Buildings.FirstOrDefault(b => b.Name == buildingName);
            if (building != null)
            {
                var floorsCount = dbContext.Floors.Count(f => f.BuildingId == building.Id);
                var classroomsCount = dbContext.Classrooms.Count(c => c.Floor.BuildingId == building.Id);
                var firstClassroom = dbContext.Classrooms.FirstOrDefault(c => c.Floor.BuildingId == building.Id);

                if (firstClassroom != null)
                {
                    await botClient.SendTextMessageAsync(chatId, $"Корпус: {buildingName}\nКількість поверхів: {floorsCount}\nКількість аудиторій: {classroomsCount}\nПерша аудиторія: {firstClassroom.Number}");
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, $"Корпус: {buildingName}\nКількість поверхів: {floorsCount}\nКількість аудиторій: {classroomsCount}\nУ цьому корпусі ще немає аудиторій.");
                }
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Корпус не знайдено.");
            }
        }
    }
}
