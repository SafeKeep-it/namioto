using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Thoth.Rendering;

namespace Thoth.Terminal;

public static class TerminalProtocolSequences
{
    public static class Csi
    {
        public const string EnableMouseClick = "\e[?1000h";
        public const string DisableMouseClick = "\e[?1000l";
        public const string EnableMouseButtonEvent = "\e[?1002h";
        public const string DisableMouseButtonEvent = "\e[?1002l";
        public const string EnableMouseAnyEvent = "\e[?1003h";
        public const string DisableMouseAnyEvent = "\e[?1003l";
        public const string EnableSgrExtendedMode = "\e[?1006h";
        public const string DisableSgrExtendedMode = "\e[?1006l";
        public const string EnableBracketedPaste = "\e[?2004h";
        public const string DisableBracketedPaste = "\e[?2004l";

        public const string EnableFixKeyboard = "\e[>1u";
        public const string DisableFixKeyboard = "\e[>0u";

        public const string EnableModifyOtherKeys = "\e[>4;1m";
        public const string DisableModifyOtherKeys = "\e[>4;0m";

        public const string EnableAlternateBuffer = "\e[?1049h";
        public const string DisableAlternateBuffer = "\e[?1049l";

        public const string EnableSyncFrame = "\e[?2026h";
        public const string DisableSyncFrame = "\e[?2026l";

        public const string HideCursor = "\e[?25l";
        public const string ShowCursor = "\e[?25h";
        public const string Home = "\e[H";
        public static ReadOnlySpan<byte> PrefixBytes => "\e["u8;
        public static ReadOnlySpan<byte> CursorPositionSuffixBytes => "H"u8;

        public static ReadOnlySpan<byte> HideCursorBytes => "\e[?25l"u8;
        public static ReadOnlySpan<byte> ShowCursorBytes => "\e[?25h"u8;
        public static ReadOnlySpan<byte> HomeBytes => "\e[H"u8;

        public static string CursorPosition(int row, int col) => $"\e[{row};{col}H";
    }

    public static class Sgr
    {
        public const string Reset = "\e[0m";
        public const string ForegroundDefault = "\e[39m";
        public const string BackgroundDefault = "\e[49m";
        public const string BoldDimOff = "\e[22m";
        public const string ItalicOff = "\e[23m";
        public const string UnderlineOff = "\e[24m";
        public const string ReverseOff = "\e[27m";
        public const string Bold = "\e[1m";
        public const string Dim = "\e[2m";
        public const string Italic = "\e[3m";
        public const string Underline = "\e[4m";
        public const string Reverse = "\e[7m";

        public const string UnderlineColorReset = "\e[59m";
        public static ReadOnlySpan<byte> SuffixBytes => "m"u8;

        public static ReadOnlySpan<byte> ResetBytes => "\e[0m"u8;
        public static ReadOnlySpan<byte> ForegroundDefaultBytes => "\e[39m"u8;
        public static ReadOnlySpan<byte> BackgroundDefaultBytes => "\e[49m"u8;
        public static ReadOnlySpan<byte> BoldDimOffBytes => "\e[22m"u8;
        public static ReadOnlySpan<byte> ItalicOffBytes => "\e[23m"u8;
        public static ReadOnlySpan<byte> UnderlineOffBytes => "\e[24m"u8;
        public static ReadOnlySpan<byte> ReverseOffBytes => "\e[27m"u8;
        public static ReadOnlySpan<byte> BoldBytes => "\e[1m"u8;
        public static ReadOnlySpan<byte> DimBytes => "\e[2m"u8;
        public static ReadOnlySpan<byte> ItalicBytes => "\e[3m"u8;
        public static ReadOnlySpan<byte> UnderlineBytes => "\e[4m"u8;
        public static ReadOnlySpan<byte> ReverseBytes => "\e[7m"u8;
        public static ReadOnlySpan<byte> UnderlineColorResetBytes => "\e[59m"u8;

        public static ReadOnlySpan<byte> ForegroundRgbPrefixBytes => "38;2;"u8;
        public static ReadOnlySpan<byte> BackgroundRgbPrefixBytes => "48;2;"u8;
        public static ReadOnlySpan<byte> UnderlineRgbPrefixBytes => "58;2;"u8;

        public static string ForegroundRgb(byte r, byte g, byte b) =>
            $"\e[38;2;{r};{g};{b}m";

        public static string BackgroundRgb(byte r, byte g, byte b) =>
            $"\e[48;2;{r};{g};{b}m";

        public static string UnderlineRgb(byte r, byte g, byte b) =>
            $"\e[58;2;{r};{g};{b}m";
    }

    public static class Osc
    {
        static readonly byte[][] setProgressBytes = BuildSetProgressBytes();

        public const string Bell = "\a";

        public static ReadOnlySpan<byte> PrefixBytes => "\e]"u8;
        public static ReadOnlySpan<byte> TerminatorBytes => "\e\\"u8;
        public static ReadOnlySpan<byte> BellBytes => "\a"u8;
        public static ReadOnlySpan<byte> ClipboardPrefixBytes => "\e]52;c;"u8;
        public static ReadOnlySpan<byte> HyperlinkPrefixBytes => "\e]8;;"u8;
        public static ReadOnlySpan<byte> HyperlinkEndBytes => "\e]8;;\e\\"u8;
        public static ReadOnlySpan<byte> ClearProgressBytes => "\e]9;4;0;0\a"u8;

        public static string SetTitle(string title) => $"\e]0;{title}{Bell}";

        public static string SetProgress(int percent)
        {
            var clamped = Math.Clamp(percent, 0, 100);
            return $"\e]9;4;2;{clamped}{Bell}";
        }

        public static string ClearProgress() => $"\e]9;4;0;0{Bell}";

        public static ReadOnlySpan<byte> SetProgressBytes(int percent)
        {
            var clamped = Math.Clamp(percent, 0, 100);
            return setProgressBytes[clamped];
        }

        public static string Hyperlink(string? url, string? id = null)
        {
            var idPart = id != null ? $"id={id}" : "";
            return $"\e]8;{idPart};{url ?? ""}\e\\";
        }

        public static string HyperlinkEnd() => "\e]8;;\e\\";

        public static void WriteClipboardCommand(Stream output, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                output.Write(ClipboardPrefixBytes);
                output.Write(BellBytes);
                return;
            }

            var utf8MaxByteCount = Math.Max(1, Encoding.UTF8.GetMaxByteCount(text.Length));
            Span<byte> utf8Stack = stackalloc byte[1024];
            using var utf8Buf = StackBuffer<byte>.Create(utf8Stack, utf8MaxByteCount);
            var utf8Buffer = utf8Buf.Span;

            var utf8Written = Encoding.UTF8.GetBytes(text, utf8Buffer);
            var base64MaxByteCount = Math.Max(4, ((utf8Written + 2) / 3) * 4);

            Span<byte> base64Stack = stackalloc byte[1024];
            using var base64Buf = StackBuffer<byte>.Create(base64Stack, base64MaxByteCount);
            var base64Buffer = base64Buf.Span;

            var status = Base64.EncodeToUtf8(utf8Buffer[..utf8Written],
                                             base64Buffer,
                                             out _,
                                             out var base64Written,
                                             isFinalBlock: true);
            if (status != OperationStatus.Done)
                throw new InvalidOperationException($"Failed to encode clipboard payload: {status}");

            output.Write(ClipboardPrefixBytes);
            output.Write(base64Buffer[..base64Written]);
            output.Write(BellBytes);
        }

        static byte[][] BuildSetProgressBytes()
        {
            var values = new byte[101][];
            for (var i = 0; i <= 100; i++)
                values[i] = Encoding.UTF8.GetBytes($"\e]9;4;2;{i}\a");

            return values;
        }
    }
}
