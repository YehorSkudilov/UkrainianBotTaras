using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkrainianBotTaras
{
    public class ConsoleWithDateTime : TextWriter
    {
        private readonly TextWriter _originalOut;

        public ConsoleWithDateTime(TextWriter originalOut)
        {
            _originalOut = originalOut;
        }

        // Capture simple WriteLine(string)
        public override void WriteLine(string? value)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _originalOut.WriteLine($"[{timestamp}] {value}");
        }

        // Capture WriteLine(string format, params object[]) used like Console.WriteLine("Val: {0}", 10)
        public override void WriteLine(string format, params object?[] arg)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formatted = string.Format(format, arg);
            _originalOut.WriteLine($"[{timestamp}] {formatted}");
        }

        // Required override for TextWriter
        public override Encoding Encoding => _originalOut.Encoding;
    }
}
