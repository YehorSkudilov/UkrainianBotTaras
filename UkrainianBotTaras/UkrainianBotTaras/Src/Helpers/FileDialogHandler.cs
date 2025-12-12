using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System;

namespace UkrainianBotTaras
{
    internal static class FileDialogHandler
    {
        public static void Handle(string filePath)
        {
            using var automation = new UIA3Automation();
            var desktop = automation.GetDesktop();

            var dialog = Retry.WhileNull(
                () => desktop.FindFirstDescendant(cf =>
                    cf.ByName("Open")
                      .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                )?.AsWindow(),
                TimeSpan.FromSeconds(10)
            ).Result;

            if (dialog == null)
                throw new Exception("File dialog not found");

            // ---- Filename textbox ----
            var fileBoxElement = dialog.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)
            );

            if (fileBoxElement == null)
                throw new Exception("Filename textbox not found");

            var fileBox = fileBoxElement.AsTextBox();
            fileBox.Text = filePath;

            // ---- Open button ----
            var openButtonElement = dialog.FindFirstDescendant(cf =>
                cf.ByName("Open")
                  .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
            );

            if (openButtonElement == null)
                throw new Exception("Open button not found");

            var openButton = openButtonElement.AsButton();
            openButton.Invoke();
        }
    }
}
