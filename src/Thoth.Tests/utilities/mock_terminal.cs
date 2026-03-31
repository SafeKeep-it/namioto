using System.Text;
using Thoth.Terminal;

namespace Comptatata.Tests.App.Cli;

public sealed class MockTerminal : ITerminal
{
    readonly Queue<byte> _input = [];
    readonly List<byte> _written = [];

    public TerminalKind Kind => TerminalKind.Visual;
    public int WindowWidth { get; set; } = 80;
    public int WindowHeight { get; set; } = 24;
    public Queue<byte> RawInput => _input;
    public string LastClipboardText { get; private set; } = string.Empty;

    public int ReadRawInput(Span<byte> buffer)
    {
        var i = 0;
        while (i < buffer.Length && _input.Count > 0)
            buffer[i++] = _input.Dequeue();
        return i;
    }

    public void WriteRaw(ReadOnlySpan<byte> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
            _written.Add(buffer[i]);
    }

    public void SetClipboard(string text) => LastClipboardText = text;

    public void QueueInput(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        for (var i = 0; i < bytes.Length; i++)
            _input.Enqueue(bytes[i]);
    }

    public void QueueInput(ReadOnlySpan<byte> input)
    {
        for (var i = 0; i < input.Length; i++)
            _input.Enqueue(input[i]);
    }

    public byte[] DrainWrittenBytes()
    {
        var result = _written.ToArray();
        _written.Clear();
        return result;
    }
}
