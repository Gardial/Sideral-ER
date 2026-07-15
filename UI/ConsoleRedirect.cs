using System;
using System.IO;
using System.Text;

namespace RandomMagicConversion;

internal sealed class ConsoleRedirectScope : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;

    public ConsoleRedirectScope(TextWriter replacement)
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        Console.SetOut(replacement);
        Console.SetError(replacement);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
    }
}

internal sealed class CallbackTextWriter : TextWriter
{
    private readonly Action<string> _onLine;
    private readonly StringBuilder _buffer = new();

    public CallbackTextWriter(Action<string> onLine)
    {
        _onLine = onLine ?? throw new ArgumentNullException(nameof(onLine));
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        if (value == '\r')
            return;

        if (value == '\n')
        {
            FlushBuffer();
            return;
        }

        _buffer.Append(value);
    }

    public override void Write(string value)
    {
        if (value == null)
            return;

        foreach (char character in value)
            Write(character);
    }

    public override void WriteLine(string value)
    {
        Write(value);
        FlushBuffer();
    }

    public override void Flush()
    {
        FlushBuffer();
    }

    private void FlushBuffer()
    {
        _onLine(_buffer.ToString());
        _buffer.Clear();
    }
}
