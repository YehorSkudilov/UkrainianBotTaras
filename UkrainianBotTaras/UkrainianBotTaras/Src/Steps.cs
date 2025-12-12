using Microsoft.Playwright;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UkrainianBotTaras.Src
{
    internal class Steps : ISteps
    {
        private readonly BotContext ctx;
        private IPage page; // Class-level variable (no underscore)

        public Steps(BotContext ctx)
        {
            this.ctx = ctx;
        }

        // ENTRY POINT
        public async Task RunAsync(IPage page)
        {
            this.page = page; // Assign to the class field

            await OpenURL(ctx.BaseUrl);
            await Click("Особистий ключ");
            await UploadFile("Оберіть ключ на своєму носієві", ctx.KeyFilePath);
            await FillInput("#id-app-login-sign-form-file-key-password", ctx.KeyPassword);
            await Click("Продовжити");
            await Click("Запис на візит");
            await Click("Записатись на візит");
            await FillInputComplex("день", ctx.DayOfBirth);
            await SelectAndEnterToDropdown("місяць", ctx.MonthOfBirth);
            await FillInputComplex("рік", ctx.YearOfBirth);
            await Click("Далі");
            SelectAndEnterToDropdown("Країна", ctx)
        }

        // ----------------------------
        // Reusable Rules
        // ----------------------------

        private async Task OpenURL(string url)
        {
            Console.WriteLine("Opening homepage...");
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        private async Task Click(string searchText)
        {
            Console.WriteLine($"Clicking {searchText}...");
            var frame = await GetActiveFrame();
            await frame.GetByText(searchText).HumanClickAsync();
        }

        private async Task UploadFile(string searchText, string filePath)
        {
            Console.WriteLine("Uploading key file...");
            var frame = await GetActiveFrame();

            var fileChooserTask = page.WaitForFileChooserAsync();

            await frame.GetByText(searchText).HumanClickAsync();

            var fileChooser = await fileChooserTask;
            await fileChooser.SetFilesAsync(filePath);
        }

        private async Task FillInput(string searchText, string input)
        {
            Console.WriteLine("Filling Input");
            var frame = await GetActiveFrame();

            var Input = frame.Locator(searchText);
            await Input.HumanFillAsync(input);
        }

        private async Task SelectAndEnterToDropdown(string labelOrQaId, string optionText)
        {
            Console.WriteLine($"Selecting '{optionText}' in dropdown '{labelOrQaId}'...");
            var frame = await GetActiveFrame();

            ILocator dropdownTrigger;

            dropdownTrigger = frame.Locator($"div[role='button']:near(:text('{labelOrQaId}'))").First;

            await dropdownTrigger.HumanClickAsync(page);

            var option = frame.Locator("li[role='option']").GetByText(optionText);

            if (!await option.IsVisibleAsync())
            {
                
                option = page.Locator("li[role='option']").GetByText(optionText);
            }

            await option.HumanClickAsync(page);
        }

        private async Task FillInputComplex(string text, string value)
        {
            Console.WriteLine($"Filling Input: {text}");
            IFrame frame = await GetActiveFrame();

            ILocator input;

            // 1. Try to find by the 'data-qa' attribute (Your HTML uses this!)
            // This turns "день" into "[data-qa='день']"
            var byDataQa = frame.Locator($"[data-qa='{text}']");

            if (await byDataQa.CountAsync() > 0)
            {
                input = byDataQa.First;
            }
            else
            {
                // 2. Fallback: Try to find by visual Label
                input = frame.GetByLabel(text).First;
            }

            // Now 'input' is valid, so BoundingBoxAsync won't timeout
            await input.HumanFillAsync(value, page);
        }

        private async Task<IFrame> GetActiveFrame()
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var frame = page.MainFrame;
            if (frame == null) throw new Exception("Auth iframe not found");
            return frame;
        }
    }
}