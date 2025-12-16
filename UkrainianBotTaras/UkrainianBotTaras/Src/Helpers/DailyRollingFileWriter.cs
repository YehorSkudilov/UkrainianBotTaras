using System.Text;
namespace UkrainianBotTaras;

    public sealed class DailyRollingFileWriter : TextWriter
{
    private readonly string _logDir;
    private StreamWriter? _writer;
    private DateTime _currentDate;

    public DailyRollingFileWriter(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(_logDir);
        OpenWriter(DateTime.Now);
    }

    public override Encoding Encoding => Encoding.UTF8;

    private void OpenWriter(DateTime date)
    {
        _currentDate = date.Date;

        _writer?.Dispose();

        string path = Path.Combine(
            _logDir,
            $"bot_{_currentDate:yyyy-MM-dd}.log"
        );

        _writer = new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)
        )
        {
            AutoFlush = true
        };
    }

    private void EnsureDate()
    {
        var now = DateTime.Now.Date;
        if (now != _currentDate)
            OpenWriter(DateTime.Now);
    }

    public override void Write(char value)
    {
        EnsureDate();
        _writer!.Write(value);
    }

    public override void Write(string? value)
    {
        EnsureDate();
        _writer!.Write(value);
    }

    public override void WriteLine(string? value)
    {
        EnsureDate();
        _writer!.WriteLine(value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _writer?.Dispose();

        base.Dispose(disposing);
    }
}
