using System.Diagnostics;
using System.Text;

namespace Thoth.Terminal.Raw.Ingress;

public sealed class InputReader
{
    const int MetaWindowMs = 50;
    const int ChunkSize = 256;
    static readonly long ScrollWindowTicks = Stopwatch.Frequency *
        (long.TryParse(Environment.GetEnvironmentVariable("THOTH_SCROLL_WINDOW_MS"), out var ms)
            ? ms
            : 24) / 1000;

    readonly InputBuffer _buffer = new();
    readonly Action<ScreenOp> _post;
    readonly ITerminal _terminal;

    ScrollCache _scrollCache;

    public InputReader(ITerminal terminal, Action<ScreenOp> post)
    {
        _terminal = terminal;
        _post = post;
    }

    public void RunReader(CancellationToken ct)
    {
        Span<byte> buf = stackalloc byte[ChunkSize];
        while (!ct.IsCancellationRequested)
        {
            var n = _terminal.ReadRawInput(buf);
            if (n > 0)
            {
                var timestamp = Stopwatch.GetTimestamp();
                _buffer.Write(buf[..n], timestamp);
            }
        }
    }

    public void RunParser(CancellationToken ct)
    {
        var parseBuffer = new byte[ChunkSize * 4];
        var parseLen = 0;
        var lastChunkTimestamp = Stopwatch.GetTimestamp();
        var metaWindowTicks = Stopwatch.Frequency * MetaWindowMs / 1000;

        while (!ct.IsCancellationRequested)
        {
            var chunk = _buffer.Read();
            if (chunk == null)
            {
                if (parseLen > 0 && parseBuffer[parseLen - 1] == 0x1B)
                {
                    var now = Stopwatch.GetTimestamp();
                    if (now - lastChunkTimestamp > metaWindowTicks)
                    {
                        _post(new(ScreenOpTarget.Editor,
                                  ScreenOpKind.EscapeKey,
                                  ScreenOpCoalesce.None,
                                  0,
                                  0));
                        parseLen--;
                    }
                }

                if (_scrollCache.Active)
                    if (TryFlushScroll(Stopwatch.GetTimestamp(), out var op) && op.HasValue)
                        _post(op.Value);

                Thread.Sleep(1);
                continue;
            }

            (var data, var length, var timestamp) = chunk.Value;

            if (parseLen + length > parseBuffer.Length)
                Array.Resize(ref parseBuffer, parseLen + length + ChunkSize);

            data.AsSpan(0, length).CopyTo(parseBuffer.AsSpan(parseLen));
            parseLen += length;
            lastChunkTimestamp = timestamp;
            _buffer.Return(data);

            parseLen = Parse(parseBuffer.AsSpan(0, parseLen));

            if (_scrollCache.Active)
                if (TryFlushScroll(Stopwatch.GetTimestamp(), out var op) && op.HasValue)
                    _post(op.Value);
        }
    }

    int Parse(Span<byte> data)
    {
        var i = 0;
        while (i < data.Length)
        {
            var b = data[i];

            if (b == 0x1B)
            {
                if (i + 1 >= data.Length)
                {
                    if (data.Length > i)
                    {
                        data[i..].CopyTo(data);
                        return data.Length - i;
                    }

                    return 0;
                }

                var consumed = ParseEscapeSequence(data[i..]);
                if (consumed == 0)
                {
                    data[i..].CopyTo(data);
                    return data.Length - i;
                }

                i += consumed;
                continue;
            }

            FlushScroll();
            i += HandleRegularKey(data[i..]);
        }

        return 0;
    }

    void FlushScroll()
    {
        if (_scrollCache.Active)
        {
            _post(CreateScrollOp());
            _scrollCache = default;
        }
    }

    bool TryFlushScroll(long now, out ScreenOp? op)
    {
        op = null;
        if (!_scrollCache.Active) return false;
        if (now - _scrollCache.LastTicks > ScrollWindowTicks)
        {
            op = CreateScrollOp();
            _scrollCache = default;
            return true;
        }

        return false;
    }

    ScreenOp CreateScrollOp() =>
        new(ScreenOpTarget.Editor,
            ScreenOpKind.MouseScroll,
            ScreenOpCoalesce.Last,
            _scrollCache.LastX,
            PackMouseB(_scrollCache.LastY, _scrollCache.Delta, 0));

    int ParseEscapeSequence(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) return 0;

        var next = data[1];

        if (next == '[') return ParseCsiSequence(data);

        FlushScroll();
        if ((next >= 'a' && next <= 'z') || (next >= 'A' && next <= 'Z'))
        {
            var ch = (char)next;
            PostKeyWithAlt(ch);
            return 2;
        }

        _post(new(ScreenOpTarget.Editor, ScreenOpKind.EscapeKey, ScreenOpCoalesce.None, 0, 0));
        return 1;
    }

    int ParseCsiSequence(ReadOnlySpan<byte> data)
    {
        // Check for bracket paste start before normal CSI parsing
        // ESC[200~ is the bracket paste start sequence
        if (data.Length >= 6 && data[2] == '2' && data[3] == '0' && data[4] == '0' &&
            data[5] == '~')
            return ParseBracketPaste(data);

        var end = 2;
        while (end < data.Length && !IsTerminator(data[end]))
        {
            end++;
        }

        if (end >= data.Length) return 0;

        var seq = data[2..end];
        var terminator = (char)data[end];

        if (seq.Length > 0 && seq[0] == '<') return ParseSgrMouse(data, end);

        FlushScroll();
        if (terminator == 'D' || terminator == 'C')
        {
            var seqStr = Encoding.ASCII.GetString(seq);
            var parts = seqStr.Split(';');
            if (parts.Length >= 2 && int.TryParse(parts[^1].TrimEnd('D', 'C'), out var mod))
            {
                var isAlt = mod == 3 || mod == 9 || mod == 11 || mod == 13;
                var isShift = mod == 2 || mod == 4 || mod == 10 || mod == 12 || mod == 14;
                if (isAlt)
                {
                    var consoleKey =
                        terminator == 'D' ? ConsoleKey.LeftArrow : ConsoleKey.RightArrow;
                    var modifiers = ConsoleModifiers.Alt | (isShift ? ConsoleModifiers.Shift : 0);
                    _post(new(ScreenOpTarget.Editor,
                              ScreenOpKind.Key,
                              ScreenOpCoalesce.None,
                              (int)consoleKey,
                              (int)modifiers));
                    return end + 1;
                }
            }

            var key = terminator == 'D' ? ConsoleKey.LeftArrow : ConsoleKey.RightArrow;
            PostKey('\0', key, 0);
            return end + 1;
        }

        if (terminator == 'A')
        {
            PostKey('\0', ConsoleKey.UpArrow, 0);
            return end + 1;
        }

        if (terminator == 'B')
        {
            PostKey('\0', ConsoleKey.DownArrow, 0);
            return end + 1;
        }

        if (terminator == 'H')
        {
            PostKey('\0', ConsoleKey.Home, 0);
            return end + 1;
        }

        if (terminator == 'F')
        {
            PostKey('\0', ConsoleKey.End, 0);
            return end + 1;
        }

        // Handle ~ terminated sequences: ESC[n~ or ESC[27;modifier;char~
        if (terminator == '~' && seq.Length > 0)
        {
            var seqStr = Encoding.ASCII.GetString(seq);
            var parts = seqStr.Split(';');
            if (parts.Length == 3 && parts[0] == "27")
                if (int.TryParse(parts[1], out var mod) && int.TryParse(parts[2], out var code))
                {
                    var modVal = mod - 1;
                    var finalMod = modVal & 4; // Control
                    if ((modVal & 1) != 0) finalMod |= 2; // Shift
                    if ((modVal & 2) != 0) finalMod |= 1; // Alt

                    var modifiers = (ConsoleModifiers)finalMod;
                    var key = code switch
                              {
                                  13 => ConsoleKey.Enter,
                                  27 => ConsoleKey.Escape,
                                  9 => ConsoleKey.Tab,
                                  127 => ConsoleKey.Backspace,
                                  var _ => ConsoleKey.NoName
                              };
                    if (key != ConsoleKey.NoName)
                    {
                        PostKey(code == 13 ? '\r' : '\0', key, (int)modifiers);
                        return end + 1;
                    }
                }

            if (int.TryParse(seqStr, out var codeSingle))
            {
                var key = codeSingle switch
                          {
                              3 => ConsoleKey.Delete,
                              5 => ConsoleKey.PageUp,
                              6 => ConsoleKey.PageDown,
                              var _ => ConsoleKey.NoName
                          };
                if (key != ConsoleKey.NoName)
                {
                    PostKey('\0', key, 0);
                    return end + 1;
                }
            }
        }

        // Handle CSI u sequences: ESC[code;modifieru
        if (terminator == 'u')
        {
            var seqStr = Encoding.ASCII.GetString(seq);
            var parts = seqStr.Split(';');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var code))
            {
                var modPart = parts[1];
                var colonIndex = modPart.IndexOf(':');
                var modStr = colonIndex >= 0 ? modPart[..colonIndex] : modPart;

                if (int.TryParse(modStr, out var mod))
                {
                    var modVal = mod - 1;
                    var finalMod = modVal & 4; // Control
                    if ((modVal & 1) != 0) finalMod |= 2; // Shift
                    if ((modVal & 2) != 0) finalMod |= 1; // Alt

                    var modifiers = (ConsoleModifiers)finalMod;
                    var key = code switch
                              {
                                  13 => ConsoleKey.Enter,
                                  27 => ConsoleKey.Escape,
                                  9 => ConsoleKey.Tab,
                                  127 => ConsoleKey.Backspace,
                                  var _ => ConsoleKey.NoName
                              };
                    if (key != ConsoleKey.NoName)
                    {
                        PostKey(code == 13 ? '\r' : '\0', key, (int)modifiers);
                        return end + 1;
                    }
                }
            }
        }

        return end + 1;
    }

    int ParseSgrMouse(ReadOnlySpan<byte> data, int end)
    {
        var terminator = (char)data[end];
        var seq = data[3..end];
        var seqStr = Encoding.ASCII.GetString(seq);
        var parts = seqStr.Split(';');

        if (parts.Length >= 3 && int.TryParse(parts[0], out var b) &&
            int.TryParse(parts[1], out var x) && int.TryParse(parts[2], out var y))
        {
            var coreBtn = b & ~32;
            var isMotion = (b & 32) == 32;
            var isRelease = terminator == 'm';

            if (b == 64 || b == 65)
            {
                var direction = b == 64 ? 1 : -1;
                var now = Stopwatch.GetTimestamp();

                if (_scrollCache.Active)
                {
                    if (direction != _scrollCache.LastSign ||
                        now - _scrollCache.LastTicks > ScrollWindowTicks)
                    {
                        _post(CreateScrollOp());
                        _scrollCache = new()
                                       {
                                           Active = true,
                                           Delta = direction,
                                           LastX = x,
                                           LastY = y,
                                           StartTicks = now,
                                           LastTicks = now,
                                           LastSign = direction
                                       };
                    }
                    else
                    {
                        _scrollCache.Delta += direction;
                        _scrollCache.LastX = x;
                        _scrollCache.LastY = y;
                        _scrollCache.LastTicks = now;
                    }
                }
                else
                {
                    _scrollCache = new()
                                   {
                                       Active = true,
                                       Delta = direction,
                                       LastX = x,
                                       LastY = y,
                                       StartTicks = now,
                                       LastTicks = now,
                                       LastSign = direction
                                   };
                }

                return end + 1;
            }

            FlushScroll();
            if (isRelease)
                _post(new(ScreenOpTarget.Editor,
                          ScreenOpKind.MouseUp,
                          ScreenOpCoalesce.Last,
                          x,
                          PackMouseB(y, coreBtn, 0)));
            else if (isMotion)
                _post(new(ScreenOpTarget.Editor,
                          ScreenOpKind.MouseMove,
                          ScreenOpCoalesce.Last,
                          x,
                          PackMouseB(y, coreBtn, 1)));
            else
                _post(new(ScreenOpTarget.Editor,
                          ScreenOpKind.MouseDown,
                          ScreenOpCoalesce.Last,
                          x,
                          PackMouseB(y, coreBtn, 0)));
        }

        return end + 1;
    }

    int ParseBracketPaste(ReadOnlySpan<byte> data)
    {
        var pasteStart = 6;
        var pasteEnd = pasteStart;

        while (pasteEnd + 5 < data.Length)
        {
            if (data[pasteEnd] == 0x1B && data[pasteEnd + 1] == '[' && data[pasteEnd + 2] == '2' &&
                data[pasteEnd + 3] == '0' && data[pasteEnd + 4] == '1' && data[pasteEnd + 5] == '~')
            {
                var pasteData =
                    Encoding.UTF8.GetString(data[pasteStart..pasteEnd]);
                _post(new(ScreenOpTarget.Editor,
                          ScreenOpKind.Paste,
                          ScreenOpCoalesce.None,
                          0,
                          0,
                          pasteData));
                return pasteEnd + 6;
            }

            pasteEnd++;
        }

        return 0;
    }

    int HandleRegularKey(ReadOnlySpan<byte> data)
    {
        var b = data[0];

        if (b == 0x0D || b == 0x0A)
        {
            // In raw mode, \r (0x0D) is Enter.
            // GUIDELINE: don't invent meaning for bare \n here.
            PostKey('\r', ConsoleKey.Enter, 0);
            return 1;
        }

        if (b == 0x7F || b == 0x08)
        {
            PostKey('\b', ConsoleKey.Backspace, 0);
            return 1;
        }

        if (b == 0x09)
        {
            PostKey('\t', ConsoleKey.Tab, 0);
            return 1;
        }

        if (b < 32)
        {
            var ctrlChar = (char)(b + 'A' - 1);
            var consoleKey = ctrlChar switch
                             {
                                 'C' => ConsoleKey.C,
                                 'V' => ConsoleKey.V,
                                 'X' => ConsoleKey.X,
                                 'A' => ConsoleKey.A,
                                 'Z' => ConsoleKey.Z,
                                 var _ => ConsoleKey.NoName
                             };
            PostKey(ctrlChar, consoleKey, (int)ConsoleModifiers.Control);
            return 1;
        }

        if (b < 128)
        {
            var ch = (char)b;
            var key = char.ToUpper(ch) switch
                      {
                          >= 'A' and <= 'Z' => ConsoleKey.A + (char.ToUpper(ch) - 'A'),
                          >= '0' and <= '9' => ConsoleKey.D0 + (ch - '0'),
                          ' ' => ConsoleKey.Spacebar,
                          var _ => ConsoleKey.NoName
                      };
            PostKey(ch, key, 0);
            return 1;
        }

        (var codepoint, var bytesConsumed) = DecodeUtf8(data);
        if (bytesConsumed > 0)
        {
            PostRune(new(codepoint));
            return bytesConsumed;
        }

        return 1;
    }

    static (int codepoint, int bytesConsumed) DecodeUtf8(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return (0, 0);

        var first = data[0];
        if ((first & 0x80) == 0) return (first, 1);
        if ((first & 0xE0) == 0xC0 && data.Length >= 2)
            return (((first & 0x1F) << 6) | (data[1] & 0x3F), 2);
        if ((first & 0xF0) == 0xE0 && data.Length >= 3)
            return (((first & 0x0F) << 12) | ((data[1] & 0x3F) << 6) | (data[2] & 0x3F), 3);
        if ((first & 0xF8) == 0xF0 && data.Length >= 4)
            return (
                ((first & 0x07) << 18) | ((data[1] & 0x3F) << 12) | ((data[2] & 0x3F) << 6) |
                (data[3] & 0x3F), 4);

        return (first, 1);
    }

    static bool IsTerminator(byte b) => b >= 0x40 && b <= 0x7E;

    void PostKey(char keyChar, ConsoleKey key, int modifiers)
    {
        var b = ((int)key & 0xFF) | ((modifiers & 0xFF) << 8);
        _post(new(ScreenOpTarget.Editor,
                  ScreenOpKind.Key,
                  GetKeyCoalescence(keyChar, key, modifiers),
                  keyChar,
                  b));
    }

    void PostKeyWithAlt(char ch)
    {
        var key = char.ToUpper(ch) switch
                  {
                      >= 'A' and <= 'Z' => ConsoleKey.A + (char.ToUpper(ch) - 'A'),
                      var _ => ConsoleKey.NoName
                  };
        PostKey(ch, key, (int)ConsoleModifiers.Alt);
    }

    void PostRune(Rune rune)
    {
        var text = rune.ToString();
        _post(new(ScreenOpTarget.Editor,
                  ScreenOpKind.Key,
                  ScreenOpCoalesce.AppendText,
                  rune.Value,
                  0,
                  text));
    }

    static ScreenOpCoalesce GetKeyCoalescence(char keyChar, ConsoleKey key, int modifiers)
    {
        if (keyChar < 32) return ScreenOpCoalesce.None;
        if ((modifiers & ((int)ConsoleModifiers.Alt | (int)ConsoleModifiers.Control)) != 0)
            return ScreenOpCoalesce.None;
        if (IsNavigationKey(key)) return ScreenOpCoalesce.None;
        return ScreenOpCoalesce.AppendText;
    }

    static bool IsNavigationKey(ConsoleKey key)
    {
        return key is ConsoleKey.LeftArrow or
            ConsoleKey.RightArrow or
            ConsoleKey.UpArrow or
            ConsoleKey.DownArrow or
            ConsoleKey.Home or
            ConsoleKey.End or
            ConsoleKey.PageUp or
            ConsoleKey.PageDown or
            ConsoleKey.Backspace or
            ConsoleKey.Delete or
            ConsoleKey.Escape or
            ConsoleKey.Tab or
            ConsoleKey.Enter;
    }

    public static ConsoleKeyInfo UnpackKey(int reservedA, int reservedB)
    {
        var keyChar = (char)reservedA;
        var key = (ConsoleKey)(reservedB & 0xFF);
        var modifiers = (ConsoleModifiers)((reservedB >> 8) & 0xFF);
        var shift = (modifiers & ConsoleModifiers.Shift) != 0;
        var alt = (modifiers & ConsoleModifiers.Alt) != 0;
        var control = (modifiers & ConsoleModifiers.Control) != 0;
        return new(keyChar, key, shift, alt, control);
    }

    public static (int a, int b) PackKey(ConsoleKeyInfo key)
    {
        var a = (int)key.KeyChar;
        var b = ((int)key.Key & 0xFF) | (((int)key.Modifiers & 0xFF) << 8);
        return (a, b);
    }

    static int PackMouseB(int y, int button, int flags) =>
        (y & 0xFFFF) | ((button & 0xFF) << 16) | ((flags & 0xFF) << 24);

    public static (int y, int button, int flags) UnpackMouseB(int b) =>
        (b & 0xFFFF, (sbyte)((b >> 16) & 0xFF), (b >> 24) & 0xFF);

    struct ScrollCache
    {
        public int Delta;
        public int LastX;
        public int LastY;
        public long StartTicks;
        public long LastTicks;
        public int LastSign;
        public bool Active;
    }
}
