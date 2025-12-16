using Microsoft.Playwright;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using UkrainianBotTaras.Src;

namespace UkrainianBotTaras;

class Program
{
    static async Task Main(string[] args)
    {
        StepsContext ctx = LoadConfig();
        SetDefaultDataDirectory(ctx);

        // --------------------------------------------
        // Console + logging setup
        // --------------------------------------------
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        string logDir = Path.Combine(ctx.DataDirectory, "Logs");
        Directory.CreateDirectory(logDir);

        var dailyFileWriter = new DailyRollingFileWriter(logDir);

        var teeWriter = new MultiTextWriter(
            Console.Out,
            dailyFileWriter
        );

        Console.SetOut(new ConsoleWithDateTime(teeWriter));
        Console.SetError(Console.Out);

        // --------------------------------------------
        // Explicit install mode (CLI)
        // --------------------------------------------
        if (args.Contains("install", StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine("🔧 Installing Playwright browsers...");
            Microsoft.Playwright.Program.Main(new[] { "install" });
            Console.WriteLine("✅ Installation complete.");
            return;
        }

        // --------------------------------------------
        // Create browser (with automatic install)
        // --------------------------------------------
        Bot bot;

        try
        {
            bot = await Bot.CreateAsync();
        }
        catch (PlaywrightException ex)
            when (ex.Message.Contains("Executable doesn't exist"))
        {
            Console.WriteLine("⚠️ Playwright browsers missing.");
            Console.WriteLine("🔧 Installing automatically...");

            Microsoft.Playwright.Program.Main(new[] { "install" });

            Console.WriteLine("✅ Installation complete. Starting browser...");
            bot = await Bot.CreateAsync(); // retry ONCE
        }

        Steps steps = new Steps(ctx);

        // --------------------------------------------
        // Run bot (Steps owns retries & cooldowns)
        // --------------------------------------------
        try
        {
            await bot.RunAsync(steps);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("💥 FATAL ERROR:");
            Console.WriteLine(ex);
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    // ------------------------------------------------
    // Helpers
    // ------------------------------------------------
    static void SetDefaultDataDirectory(StepsContext ctx)
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
