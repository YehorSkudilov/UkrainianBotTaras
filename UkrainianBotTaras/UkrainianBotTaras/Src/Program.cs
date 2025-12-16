using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UkrainianBotTaras.Src;

namespace UkrainianBotTaras;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        // 1. Load the global configuration file
        GlobalConfig config = LoadGlobalConfig();

        // Check if a template exists and assign base directory
        if (config.TemplateContext == null)
            throw new InvalidOperationException("Configuration file must contain a 'TemplateContext'.");

        SetDefaultBotInstanceDirectory(config.TemplateContext);

        // 2. Setup GLOBAL Console Output (only directs to Console)
        // We REMOVE the global file writer here.
        Console.SetOut(new ConsoleWithDateTime(Console.Out));
        Console.SetError(Console.Out);

        try
        {
            if (args.Contains("install", StringComparer.OrdinalIgnoreCase))
            {
                Microsoft.Playwright.Program.Main(new[] { "install" });
                return;
            }

            // 3. Orchestrate the parallel bot execution
            await RunBotsInParallel(config);
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

    // -----------------------------------------------------------
    // NEW CORE LOGIC FOR PARALLEL EXECUTION AND ISOLATED LOGGING
    // -----------------------------------------------------------

    static async Task RunBotsInParallel(GlobalConfig config)
    {
        Console.WriteLine($"Starting parallel launch of {config.BotsCount} bots with a {config.LaunchDelayMs}ms stagger.");

        var botTasks = new List<Task>();

        // Call SetBaseDataDirectory on the TEMPLATE to guarantee ctx.DataDirectory is NOT null.
        // This is the CRITICAL FIX for the ArgumentNullException.
        SetBaseDataDirectory(config.TemplateContext!);

        for (int i = 0; i < config.BotsCount; i++)
        {
            // 1. Clone the template context for isolation
            StepsContext uniqueCtx = config.TemplateContext!.Clone();

            // 2. Create unique subdirectory and set paths. 
            // This method will now correctly use the non-null ctx.DataDirectory base path.
            SetUniqueBotInstanceDirectory(uniqueCtx, i);

            // ... rest of the loop remains the same ...
            int initialDelayMs = config.LaunchDelayMs * i;
            var botTask = LaunchAndRunBotAsync(uniqueCtx, initialDelayMs);
            botTasks.Add(botTask);
        }

        await Task.WhenAll(botTasks);
        Console.WriteLine("All bot instances have completed their runs.");
    }

    static async Task LaunchAndRunBotAsync(StepsContext ctx, int initialDelayMs)
    {
        // 1. Setup Isolated Logging for this specific bot instance
        var logDir = Path.Combine(ctx.BotInstanceDirectory, "Logs");
        Directory.CreateDirectory(logDir);

        // This log file is unique to the bot instance (e.g., Data/BotInstance_1/Logs/...)
        var botFileWriter = new DailyRollingFileWriter(logDir);

        // This writer sends output to BOTH the main console (via Console.Out) AND the bot's private log file.
        var botTeeWriter = new MultiTextWriter(Console.Out, botFileWriter);

        // Use a local ConsoleWithDateTime instance to direct output for THIS task
        var isolatedConsole = new ConsoleWithDateTime(botTeeWriter);

        // 2. Logging startup message and initial delay
        if (initialDelayMs > 0)
        {
            isolatedConsole.WriteLine($"Bot {ctx.BotId}: Waiting {initialDelayMs}ms before launch...");
            await Task.Delay(initialDelayMs);
        }

        // 3. Execution Loop
        bool installAttempted = false;

        while (true)
        {
            Bot? bot = null;

            try
            {
                // Temporarily redirect the console output for THIS thread to our isolated writer
                // This ensures all subsequent Console.WriteLine calls within this task go to both destinations
                // This approach requires that your application code does not rely heavily on the thread-static nature of Console.Out
                TextWriter originalOut = Console.Out;
                Console.SetOut(isolatedConsole);

                try
                {
                    bot = await Bot.CreateAsync();
                    var steps = new Steps(ctx);

                    await bot.RunAsync(steps);

                    // If RunAsync completes without error, break the retry loop
                    break;
                }
                finally
                {
                    // Restore the console output to the original writer before exiting the inner block
                    Console.SetOut(originalOut);
                }
            }
            catch (Microsoft.Playwright.PlaywrightException ex)
                when (ex.Message.Contains("Executable doesn't exist at"))
            {
                // Note: Playwright install is generally sequential and shouldn't be run in parallel,
                // but this logic should remain safe for a single run.
                if (installAttempted)
                {
                    isolatedConsole.WriteLine($"Bot {ctx.BotId}: Playwright install already attempted. Waiting...");
                    await Task.Delay(5000);
                    continue;
                }

                installAttempted = true;
                isolatedConsole.WriteLine($"Bot {ctx.BotId}: Playwright browsers missing. Installing...");
                // Keep the Playwright install call as-is, as it handles its own console output
                Microsoft.Playwright.Program.Main(new[] { "install" });
                isolatedConsole.WriteLine($"Bot {ctx.BotId}: Installation complete. Restarting...");
                continue;
            }
            catch (Exception ex)
            {
                isolatedConsole.WriteLine($"Bot {ctx.BotId} caught error: {ex.Message}. Retrying...");
                await Task.Delay(2000);
                continue;
            }
            finally
            {
                // Ensure browser instance is disposed
                if (bot != null)
                {
                    await bot.DisposeAsync();
                }
                // Ensure the file writer is closed/disposed properly once the bot task is truly done.
                if (botFileWriter is IDisposable disposableWriter)
                {
                    disposableWriter.Dispose();
                }
            }
        }
    }

    // -----------------------------------------------------------
    // CONFIGURATION AND DIRECTORY HELPERS
    // -----------------------------------------------------------

    // ... (SetDefaultBotInstanceDirectory, SetUniqueBotInstanceDirectory, LoadGlobalConfig remain unchanged) ...

    static private void SetDefaultBotInstanceDirectory(StepsContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.BotInstanceDirectory))
            return;

        string exeDir = AppContext.BaseDirectory;
        string dataDir = Path.Combine(exeDir, "Data");

        Directory.CreateDirectory(dataDir);

        ctx.BotInstanceDirectory = dataDir;
    }

    static private void SetBaseDataDirectory(StepsContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.DataDirectory))
            return;

        string exeDir = AppContext.BaseDirectory;
        string dataDir = Path.Combine(exeDir, "Data"); // This is the base "Data" folder

        Directory.CreateDirectory(dataDir);

        ctx.DataDirectory = dataDir;
    }

    static private string SetUniqueBotInstanceDirectory(StepsContext ctx, int index)
    {

        // Create a unique subdirectory for this specific bot context
        string uniqueSuffix = $"BotInstance_{index + 1}";
        string uniqueDir = Path.Combine(ctx.DataDirectory, uniqueSuffix);

        Directory.CreateDirectory(uniqueDir);

        // Ensure the context knows its new, unique directory
        ctx.BotInstanceDirectory = uniqueDir;

        // Assign the unique ID for logging readability
        ctx.BotId = uniqueSuffix;

        return uniqueDir;
    }

    static GlobalConfig LoadGlobalConfig()
    {
        const string file = "botsettings.json";

        if (!File.Exists(file))
            throw new FileNotFoundException($"Config file not found: {file}");

        string json = File.ReadAllText(file);

        // Deserialize to the GlobalConfig structure
        return JsonSerializer.Deserialize<GlobalConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException($"Failed to deserialize configuration from {file}.");
    }
}