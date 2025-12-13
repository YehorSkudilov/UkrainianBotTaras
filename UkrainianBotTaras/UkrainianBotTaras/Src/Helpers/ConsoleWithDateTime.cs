using System;
using System.IO;
using System.Text;

namespace UkrainianBotTaras
{
    public class ConsoleWithDateTime : TextWriter
    {
        private readonly TextWriter _originalOut;

        public ConsoleWithDateTime(TextWriter originalOut)
        {
            _originalOut = originalOut;
        }

        public override void WriteLine(string? value)
        {
            // CHANGED HERE: Added date format
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _originalOut.WriteLine($"[{timestamp}] {value}");
        }

        public override void WriteLine(string format, params object?[] arg)
        {
            // CHANGED HERE: Added date format
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string formatted = string.Format(format, arg);
            _originalOut.WriteLine($"[{timestamp}] {formatted}");
        }

        public override Encoding Encoding => _originalOut.Encoding;
    }
}