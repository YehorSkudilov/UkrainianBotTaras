using Microsoft.Playwright;
using System;
using System.ComponentModel.Design.Serialization;
using System.Threading.Tasks;
using UkrainianBotTaras.Src;


namespace UkrainianBotTaras;
class Program
{
    static Bot bot;
    static Steps steps;
    static async Task Main()
    {
        var ctx = new BotContext
        {
            BaseUrl = "https://id.e-consul.gov.ua/",
            KeyFilePath = @"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\Завірюха_А_Р_monoКЕП_2025-12-11T213412.pfx",
            KeyPassword = "$hzhehs&Q6"
        };

        bot = await Bot.CreateAsync();
        steps = new Steps(ctx);

        await bot.RunAsync(steps);

        Console.ReadLine();
    }
   
}
