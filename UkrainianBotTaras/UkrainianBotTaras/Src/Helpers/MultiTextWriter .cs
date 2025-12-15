using System.Text;

namespace UkrainianBotTaras
{
    public sealed class MultiTextWriter : TextWriter
    {
        private readonly TextWriter[] _writers;

        public MultiTextWriter(params TextWriter[] writers)
        {
            _writers = writers;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            foreach (var w in _writers)
                w.Write(value);
        }

        public override void Write(string? value)
        {
            foreach (var w in _writers)
                w.Write(value);
        }

        public override void WriteLine(string? value)
        {
            foreach (var w in _writers)
                w.WriteLine(value);
        }

        public override void Flush()
        {
            foreach (var w in _writers)
                w.Flush();
        }
    }
}
