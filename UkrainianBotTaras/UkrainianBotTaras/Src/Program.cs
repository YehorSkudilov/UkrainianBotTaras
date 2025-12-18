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
        StepsContext ctx = LoadConfig();
        SetDefaultDataDirectory(ctx);


        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        string logDir = Path.Combine(ctx.DataDirectory, "Logs");

        var dailyFileWriter = new DailyRollingFileWriter(logDir);

        var teeWriter = new MultiTextWriter(
            Console.Out,
            dailyFileWriter
        );

        Console.SetOut(new ConsoleWithDateTime(teeWriter));
        Console.SetError(Console.Out);


        try
        {
            // --------------------------------------------
            // Explicit install mode
            // --------------------------------------------
            if (args.Contains("install", StringComparer.OrdinalIgnoreCase))
            {
                Microsoft.Playwright.Program.Main(new[] { "install" });
                return;
            }



            bool installAttempted = false;

            // --------------------------------------------
            // Main self-healing loop
            // --------------------------------------------
            while (true)
            {
                Bot? bot = null;

                try
                {
                    bot = await Bot.CreateAsync(ctx);
                    var steps = new Steps(ctx);

                    await bot.RunAsync(steps);
                }
                catch (Microsoft.Playwright.PlaywrightException ex)
                    when (ex.Message.Contains("Executable doesn't exist at"))
                {
                    if (installAttempted)
                    {
                        Console.WriteLine("Playwright install already attempted. Waiting...");
                        await Task.Delay(5000);
                        continue;
                    }

                    installAttempted = true;

                    Console.WriteLine("Playwright browsers missing. Installing...");
                    Microsoft.Playwright.Program.Main(new[] { "install" });
                    Console.WriteLine("Installation complete. Restarting...");
                    continue;
                }
                catch (Microsoft.Playwright.PlaywrightException ex)
                {
                    Console.WriteLine($"Playwright error: {ex.Message}");
                    await Task.Delay(2000);
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled error: {ex}");
                    await Task.Delay(2000);
                    continue;
                }
                finally
                {
                    if (bot != null)
                    {
                        await bot.DisposeAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("FATAL ERROR:");
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
