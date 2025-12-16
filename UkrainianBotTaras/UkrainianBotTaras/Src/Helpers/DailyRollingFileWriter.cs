using System.Text;
using System.IO;
using System;

namespace UkrainianBotTaras;

public sealed class DailyRollingFileWriter : TextWriter
{
    private readonly string _logDir;
    // Use volatile to prevent compiler optimizations that could affect multithreading
    private volatile StreamWriter? _writer;
    private DateTime _currentDate;
    private readonly object _lock = new object();

    public DailyRollingFileWriter(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(_logDir);
        // Ensure the initial writer is opened under lock
        lock (_lock)
        {
            OpenWriterInternal(DateTime.Now);
        }
    }

    public override Encoding Encoding => Encoding.UTF8;

    private void OpenWriterInternal(DateTime date)
    {
        // This method is ONLY called from inside a lock!

        _currentDate = date.Date;

        // 1. Create the NEW writer first.
        string path = Path.Combine(
            _logDir,
            $"bot_{_currentDate:yyyy-MM-dd}.log"
        );

        StreamWriter newWriter = new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)
        )
        {
            AutoFlush = true
        };

        // 2. Atomically replace the reference.
        StreamWriter? oldWriter = _writer;
        _writer = newWriter;

        // 3. Dispose the old writer after the replacement.
        oldWriter?.Dispose();
    }

    private void EnsureWriterIsValid()
    {
        bool needsReopen = false;

        // Check 1: Has the writer been prematurely disposed/nulled?
        if (_writer == null)
        {
            needsReopen = true;
        }
        // Check 2: Has the date rolled over?
        else if (DateTime.Now.Date != _currentDate)
        {
            needsReopen = true;
        }

        if (needsReopen)
        {
            // Only acquire the lock when we suspect a new writer is needed (rare event)
            lock (_lock)
            {
                // Double check inside the lock for both conditions (to handle multiple waiting threads)
                if (_writer == null || DateTime.Now.Date != _currentDate)
                {
                    OpenWriterInternal(DateTime.Now);
                }
            }
        }
    }

    // Wrap ALL public write methods with the lock
    public override void Write(char value)
    {
        // For performance, the date check happens first, outside the lock, 
        // relying on the atomic assignment of _writer.
        EnsureWriterIsValid();

        _writer!.Write(value); // We rely on the atomic swap to ensure _writer is valid here.
    }

    public override void Write(string? value)
    {
        EnsureWriterIsValid();

        _writer!.Write(value);
    }

    public override void WriteLine(string? value)
    {
        EnsureWriterIsValid();

        _writer!.WriteLine(value); // This is where your exception occurs!
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
            {
                // Ensure writer is closed
                _writer?.Dispose();
                _writer = null; // Set to null to signal that the writer is permanently closed
            }
        }
        base.Dispose(disposing);
    }
}