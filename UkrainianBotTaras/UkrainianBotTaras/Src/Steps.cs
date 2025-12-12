using Microsoft.Playwright;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UkrainianBotTaras.Src
{
    internal class Steps : ISteps
    {
        private readonly BotContext ctx;

        public Steps(BotContext ctx)
        {
            this.ctx = ctx;
        }

        public async Task RunAsync(IPage page)
        {
            await OpenHomepage(page);
            await ClickPersonalKey(page);
            await UploadKeyFile(page);
            await FillKeyPassword(page);
            await ClickConfirm(page);
            // await KeepAlive();
        }

        // ----------------------------
        // STEPS
        // ----------------------------

        private async Task OpenHomepage(IPage page)
        {
            Console.WriteLine("Opening homepage...");
            await page.GotoAsync(ctx.BaseUrl);
            // Wait for visual stability
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        private async Task ClickPersonalKey(IPage page)
        {
            Console.WriteLine("Clicking Особистий ключ...");

            var frame = GetAuthFrame(page);

            // USE HUMAN CLICK
            await frame.GetByText("Особистий ключ").HumanClickAsync();
        }

        private async Task UploadKeyFile(IPage page)
        {
            Console.WriteLine("Uploading key file...");
            var frame = GetAuthFrame(page);

            // 1. Prepare to catch the file dialog
            var fileChooserTask = page.WaitForFileChooserAsync();

            // 2. Click specifically the blue text seen in your screenshot
            // We use partial text matching in case of minor spacing differences
            await frame.GetByText("Оберіть ключ на своєму носієві").HumanClickAsync();

            // 3. Upload the file
            var fileChooser = await fileChooserTask;
            await fileChooser.SetFilesAsync(ctx.KeyFilePath);
        }

        private async Task FillKeyPassword(IPage page)
        {
            Console.WriteLine("Filling password...");
            var frame = GetAuthFrame(page);

            // Convert Frame.FillAsync to Locator.HumanFillAsync
            var passwordInput = frame.Locator("#id-app-login-sign-form-file-key-password");

            // USE HUMAN FILL
            await passwordInput.HumanFillAsync(ctx.KeyPassword);
        }

        private async Task ClickConfirm(IPage page)
        {
            Console.WriteLine("Clicking Продовжити...");
            var frame = GetAuthFrame(page);

            var confirmBtn = frame.Locator("#id-app-login-sign-form-file-key-sign-button");

            // USE HUMAN CLICK
            await confirmBtn.HumanClickAsync();
        }

        // Helper to keep code clean since you check for the frame often
        private IFrame GetAuthFrame(IPage page)
        {
            var frame = page.Frames.FirstOrDefault(f => f.Url.Contains("id.e-consul.gov.ua"));
            if (frame == null) throw new Exception("Auth iframe not found");
            return frame;
        }

        private Task KeepAlive()
        {
            Console.WriteLine("Keeping browser alive...");
            return Task.Delay(-1);
        }
    }
}