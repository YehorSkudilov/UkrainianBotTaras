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
            await FillInput("день", "08");
            await FillInput("день", "05");
            await FillInput("день", "08");
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
            Console.WriteLine("Filling password...");
            var frame = await GetActiveFrame();

            var passwordInput = frame.Locator(searchText);
            await passwordInput.HumanFillAsync(input);
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