using Microsoft.Playwright;
using System;
using System.ComponentModel.Design.Serialization;
using System.Text;
using System.Threading.Tasks;
using UkrainianBotTaras.Src;


namespace UkrainianBotTaras;
class Program
{
    static Bot bot;
    static Steps steps;
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.SetOut(new ConsoleWithDateTime(Console.Out));

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dataDir = Path.Combine(baseDir, "Data");
        Directory.CreateDirectory(dataDir);

        var ctx = new StepsContext
        {
            BaseUrl = "https://id.e-consul.gov.ua/",
            KeyFilePath = Path.Combine(dataDir, "Key.pfx"),
            KeyPassword = "$hzhehs&Q6",
            DataDir = dataDir,
            DayOfBirth = "08",
            MonthOfBirth = "травня",
            YearOfBirth = "2006",
            Country = "Канада",
            //Country = "Італія",
            ConsulateName = "Генеральне Консульство України в Едмонтоні",
            //ConsulateName = "Генеральне консульство України в Мілані",
            ServiceName = "Оформлення закордонного паспорта",
        };



        while (true)
        {
            bot = await Bot.CreateAsync();
            steps = new Steps(ctx);
            await bot.RunAsync(steps);
        }

    }
}
