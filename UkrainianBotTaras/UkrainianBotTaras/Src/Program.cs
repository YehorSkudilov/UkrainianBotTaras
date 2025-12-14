using Microsoft.Playwright;
using System;
using System.ComponentModel.Design.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UkrainianBotTaras.Src;


namespace UkrainianBotTaras;
class Program
{
    static Bot bot;
    static Steps steps;
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.SetOut(new ConsoleWithDateTime(Console.Out));

        try
        {
            // Explicit install mode
            if (args.Contains("install"))
            {
                Microsoft.Playwright.Program.Main(new[] { "install" });
                return;
            }

            StepsContext ctx = LoadConfig();
            SetDefaultDataDirectory(ctx);

            bool installAttempted = false;

            while (true)
            {
                try
                {
                    bot = await Bot.CreateAsync();
                    steps = new Steps(ctx);


                    await bot.RunAsync(steps);
                }
                catch (Microsoft.Playwright.PlaywrightException ex)
                {
                    if (ex.Message.Contains("Executable doesn't exist at"))
                    {
                        installAttempted = true;

                        Console.WriteLine("Playwright browsers missing. Installing...");
                        Microsoft.Playwright.Program.Main(new[] { "install" });
                        Console.WriteLine("Installation complete. Restarting...");
                    }
                    else
                    {
                        Console.WriteLine(ex.Message);
                    }

                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR:");
            Console.WriteLine(ex);
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static private void SetDefaultDataDirectory(StepsContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.DataDirectory))
            return;

        string exeDir = AppContext.BaseDirectory;
        string dataDir = Path.Combine(exeDir, "Data");

        Directory.CreateDirectory(dataDir);

        ctx.DataDirectory = dataDir;
            
    }

    //static bool IsMissingBrowserError(Microsoft.Playwright.PlaywrightException ex)
    //{
    //    var msg = ex.Message ?? "";

    //    return msg.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
    //        || msg.Contains("Looks like Playwright was just installed or updated", StringComparison.OrdinalIgnoreCase)
    //        || msg.Contains("ms-playwright", StringComparison.OrdinalIgnoreCase)
    //        || msg.Contains("chromium-", StringComparison.OrdinalIgnoreCase)
    //        || msg.Contains("webkit-", StringComparison.OrdinalIgnoreCase)
    //        || msg.Contains("firefox-", StringComparison.OrdinalIgnoreCase);
    //}


    static StepsContext LoadConfig()
    {
        const string file = "botsettings.json";

        if (!File.Exists(file))
            throw new FileNotFoundException($"Config file not found: {file}");

        string json = File.ReadAllText(file);

        return JsonSerializer.Deserialize<StepsContext>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
    }
}
