using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Widgets;

namespace Thoth.Terminal.Raw.Egress;

public sealed class TerminalScribe(ITerminal terminal)
{
    readonly Dictionary<int, byte[]> _cursorPositionCache = [];
    readonly bool _useOscProgress =
        string.Equals(Environment.GetEnvironmentVariable("THOTH_OSC_PROGRESS")?.Trim(),
                      "1",
                      StringComparison.OrdinalIgnoreCase);
    bool _hasOscProgress;
    int _lastOscProgress = -1;
    int _cursorCacheWidth = -1;
    int _cursorCacheHeight = -1;

    public int Width => terminal.WindowWidth;
    public int Height => terminal.WindowHeight;

    public void Render(GridBuffer screen,
                       RenderContext context,
                       ushort frameNumber,
                       bool renderFullFrame)
    {
        EnsureCursorCacheBounds(screen.Width, screen.Height);

        if (!renderFullFrame)
        {
            RenderChangedCells(screen, context, frameNumber);
            return;
        }

        // Estimates: ~64 bytes per cell for ANSI + content.
        var bufferSize = screen.Width * screen.Height * 64;
        var poolBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        var pos = 0;

        try
        {
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.EnableSyncFrame);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.HideCursorBytes);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.HomeBytes);

            var hasStyle = false;
            var lastStyleIndex = int.MinValue;
            Rendering.Style lastStyle = default;

            for (var y = 0; y < screen.Height; y++)
            {
                Write(poolBuffer, ref pos, GetCursorPositionSequence(y + 1, 1));
                for (var x = 0; x < screen.Width;)
                {
                    var cell = screen.GetCell(x, y);
                    if (cell.Width == 0)
                    {
                        x++;
                        continue;
                    }

                    if (!hasStyle || cell.StyleIndex != lastStyleIndex)
                    {
                        var style = context.Styles.TryGet(cell.StyleIndex, out var resolvedStyle)
                            ? resolvedStyle
                            : default;
                        ApplyStyleTransition(poolBuffer,
                                             ref pos,
                                             style,
                                             lastStyle,
                                             force: !hasStyle);
                        lastStyle = style;
                        lastStyleIndex = cell.StyleIndex;
                        hasStyle = true;
                    }

                    var runLength = 1;
                    if (CanRunLengthEncode(cell))
                    {
                        while (x + runLength < screen.Width)
                        {
                            var next = screen.GetCell(x + runLength, y);
                            if (next.Width != 1) break;
                            if (next.StyleIndex != cell.StyleIndex) break;
                            if (next.GlyphId != cell.GlyphId) break;
                            runLength++;
                        }
                    }

                    if (cell.GlyphId >= 0)
                    {
                        var rune = new Rune(cell.GlyphId);
                        if (rune.IsAscii)
                            poolBuffer[pos++] = (byte)rune.Value;
                        else
                            pos += rune.EncodeToUtf8(poolBuffer.AsSpan(pos));
                    }
                    else
                    {
                        var cluster = context.Glyphs.Get(~cell.GlyphId);
                        pos += Encoding.UTF8.GetBytes(cluster, poolBuffer.AsSpan(pos));
                    }

                    if (runLength > 1)
                        WriteRepeatPrevious(poolBuffer, ref pos, runLength - 1);

                    x += runLength;
                }
            }

            Write(poolBuffer, ref pos, TerminalProtocolSequences.Sgr.ResetBytes);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Osc.HyperlinkEndBytes);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Sgr.UnderlineColorResetBytes);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.HomeBytes);
            _ = WriteProgressSequence(poolBuffer, ref pos, context);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.DisableSyncFrame);

            terminal.WriteRaw(poolBuffer.AsSpan(0, pos));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(poolBuffer);
        }
    }

    void RenderChangedCells(GridBuffer screen, RenderContext context, ushort frameNumber)
    {
        var bufferSize = screen.Width * screen.Height * 64;
        var poolBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        var pos = 0;

        try
        {
            var wroteAny = false;
            var hasStyle = false;
            var lastStyleIndex = int.MinValue;
            Rendering.Style lastStyle = default;

            for (var y = 0; y < screen.Height; y++)
            {
                for (var x = 0; x < screen.Width;)
                {
                    var cell = screen.GetCell(x, y);
                    if (cell.Frame != frameNumber || cell.Width == 0)
                    {
                        x++;
                        continue;
                    }

                    if (!wroteAny)
                    {
                        Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.HideCursorBytes);
                        wroteAny = true;
                    }

                    Write(poolBuffer, ref pos, GetCursorPositionSequence(y + 1, x + 1));

                    if (!hasStyle || cell.StyleIndex != lastStyleIndex)
                    {
                        var style = context.Styles.TryGet(cell.StyleIndex, out var resolvedStyle)
                            ? resolvedStyle
                            : default;
                        ApplyStyleTransition(poolBuffer,
                                             ref pos,
                                             style,
                                             lastStyle,
                                             force: !hasStyle);
                        lastStyle = style;
                        lastStyleIndex = cell.StyleIndex;
                        hasStyle = true;
                    }

                    var runLength = 1;
                    if (CanRunLengthEncode(cell))
                    {
                        while (x + runLength < screen.Width)
                        {
                            var next = screen.GetCell(x + runLength, y);
                            if (next.Frame != frameNumber) break;
                            if (next.Width != 1) break;
                            if (next.StyleIndex != cell.StyleIndex) break;
                            if (next.GlyphId != cell.GlyphId) break;
                            runLength++;
                        }
                    }

                    if (cell.GlyphId >= 0)
                    {
                        var rune = new Rune(cell.GlyphId);
                        if (rune.IsAscii)
                            poolBuffer[pos++] = (byte)rune.Value;
                        else
                            pos += rune.EncodeToUtf8(poolBuffer.AsSpan(pos));
                    }
                    else
                    {
                        var cluster = context.Glyphs.Get(~cell.GlyphId);
                        pos += Encoding.UTF8.GetBytes(cluster, poolBuffer.AsSpan(pos));
                    }

                    if (runLength > 1)
                        WriteRepeatPrevious(poolBuffer, ref pos, runLength - 1);

                    x += runLength;
                }
            }

            var wroteProgress = WriteProgressSequence(poolBuffer, ref pos, context);
            if (!wroteAny && !wroteProgress) return;

            Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.EnableSyncFrame);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Sgr.ResetBytes);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Osc.HyperlinkEndBytes);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Sgr.UnderlineColorResetBytes);
            Write(poolBuffer, ref pos, TerminalProtocolSequences.Csi.DisableSyncFrame);

            terminal.WriteRaw(poolBuffer.AsSpan(0, pos));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(poolBuffer);
        }
    }

    static void Write(byte[] buffer, ref int pos, string s)
    {
        pos += Encoding.UTF8.GetBytes(s, buffer.AsSpan(pos));
    }

    static void Write(byte[] buffer, ref int pos, ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(buffer.AsSpan(pos));
        pos += bytes.Length;
    }

    void EnsureCursorCacheBounds(int width, int height)
    {
        if (_cursorCacheWidth == width && _cursorCacheHeight == height) return;

        _cursorCacheWidth = width;
        _cursorCacheHeight = height;
        _cursorPositionCache.Clear();
    }

    byte[] GetCursorPositionSequence(int row, int col)
    {
        var key = (row << 16) | (col & 0xFFFF);
        if (_cursorPositionCache.TryGetValue(key, out var cached)) return cached;

        var sequence = BuildCursorPositionSequence(row, col);
        _cursorPositionCache[key] = sequence;
        return sequence;
    }

    static void ApplyStyleTransition(byte[] buffer,
                                     ref int pos,
                                     Rendering.Style current,
                                     Rendering.Style previous,
                                     bool force)
    {
        if (force || current.Hyperlink != previous.Hyperlink)
        {
            if (current.Hyperlink is { } hyperlink)
                WriteOscHyperlink(buffer, ref pos, hyperlink);
            else
                Write(buffer, ref pos, TerminalProtocolSequences.Osc.HyperlinkEndBytes);
        }

        if (force)
            Write(buffer, ref pos, TerminalProtocolSequences.Sgr.ResetBytes);

        if (force || current.Foreground != previous.Foreground)
        {
            if (current.Foreground is { } foreground)
                WriteSgrRgb(buffer,
                            ref pos,
                            TerminalProtocolSequences.Sgr.ForegroundRgbPrefixBytes,
                            foreground.R,
                            foreground.G,
                            foreground.B);
            else
                Write(buffer, ref pos, TerminalProtocolSequences.Sgr.ForegroundDefaultBytes);
        }

        if (force || current.Background != previous.Background)
        {
            if (current.Background is { } background)
                WriteSgrRgb(buffer,
                            ref pos,
                            TerminalProtocolSequences.Sgr.BackgroundRgbPrefixBytes,
                            background.R,
                            background.G,
                            background.B);
            else
                Write(buffer, ref pos, TerminalProtocolSequences.Sgr.BackgroundDefaultBytes);
        }

        if (force || current.UnderlineColor != previous.UnderlineColor)
        {
            if (current.UnderlineColor is { } underlineColor)
                WriteSgrRgb(buffer,
                            ref pos,
                            TerminalProtocolSequences.Sgr.UnderlineRgbPrefixBytes,
                            underlineColor.R,
                            underlineColor.G,
                            underlineColor.B);
            else
                Write(buffer, ref pos, TerminalProtocolSequences.Sgr.UnderlineColorResetBytes);
        }

        ApplyAttributes(buffer, ref pos, current.Attributes, previous.Attributes, force);
    }

    static void ApplyAttributes(byte[] buffer,
                                ref int pos,
                                Rendering.TextAttributes current,
                                Rendering.TextAttributes previous,
                                bool force)
    {
        var currentBold = (current & Rendering.TextAttributes.Bold) != 0;
        var currentDim = (current & Rendering.TextAttributes.Dim) != 0;
        var previousBold = (previous & Rendering.TextAttributes.Bold) != 0;
        var previousDim = (previous & Rendering.TextAttributes.Dim) != 0;

        if (force || currentBold != previousBold || currentDim != previousDim)
        {
            if (!force && (previousBold || previousDim))
                Write(buffer, ref pos, TerminalProtocolSequences.Sgr.BoldDimOffBytes);

            if (currentBold)
                Write(buffer, ref pos, TerminalProtocolSequences.Sgr.BoldBytes);
            if (currentDim)
                Write(buffer, ref pos, TerminalProtocolSequences.Sgr.DimBytes);
        }

        ApplySingleAttribute(buffer,
                             ref pos,
                             current,
                             previous,
                             force,
                             Rendering.TextAttributes.Italic,
                             TerminalProtocolSequences.Sgr.ItalicBytes,
                             TerminalProtocolSequences.Sgr.ItalicOffBytes);
        ApplySingleAttribute(buffer,
                             ref pos,
                             current,
                             previous,
                             force,
                             Rendering.TextAttributes.Underline,
                             TerminalProtocolSequences.Sgr.UnderlineBytes,
                             TerminalProtocolSequences.Sgr.UnderlineOffBytes);
        ApplySingleAttribute(buffer,
                             ref pos,
                             current,
                             previous,
                             force,
                             Rendering.TextAttributes.Reverse,
                             TerminalProtocolSequences.Sgr.ReverseBytes,
                             TerminalProtocolSequences.Sgr.ReverseOffBytes);
    }

    static void ApplySingleAttribute(byte[] buffer,
                                     ref int pos,
                                     Rendering.TextAttributes current,
                                     Rendering.TextAttributes previous,
                                     bool force,
                                     Rendering.TextAttributes flag,
                                     ReadOnlySpan<byte> onBytes,
                                     ReadOnlySpan<byte> offBytes)
    {
        var hasCurrent = (current & flag) != 0;
        var hadPrevious = (previous & flag) != 0;

        if (!force && hasCurrent == hadPrevious) return;

        if (hasCurrent)
            Write(buffer, ref pos, onBytes);
        else if (hadPrevious)
            Write(buffer, ref pos, offBytes);
    }

    static byte[] BuildCursorPositionSequence(int row, int col)
    {
        return Encoding.UTF8.GetBytes(TerminalProtocolSequences.Csi.CursorPosition(row, col));
    }

    static void WriteSgrRgb(byte[] buffer,
                            ref int pos,
                            ReadOnlySpan<byte> prefix,
                            byte r,
                            byte g,
                            byte b)
    {
        Write(buffer, ref pos, TerminalProtocolSequences.Csi.PrefixBytes);
        Write(buffer, ref pos, prefix);
        WriteNumber(buffer, ref pos, r);
        Write(buffer, ref pos, ";"u8);
        WriteNumber(buffer, ref pos, g);
        Write(buffer, ref pos, ";"u8);
        WriteNumber(buffer, ref pos, b);
        Write(buffer, ref pos, TerminalProtocolSequences.Sgr.SuffixBytes);
    }

    static void WriteOscHyperlink(byte[] buffer, ref int pos, string url)
    {
        Write(buffer, ref pos, TerminalProtocolSequences.Osc.HyperlinkPrefixBytes);
        Write(buffer, ref pos, url);
        Write(buffer, ref pos, TerminalProtocolSequences.Osc.TerminatorBytes);
    }

    static void WriteNumber(byte[] buffer, ref int pos, int value)
    {
        if (!Utf8Formatter.TryFormat(value, buffer.AsSpan(pos), out var written))
            throw new InvalidOperationException("Failed to format ANSI numeric segment.");
        pos += written;
    }

    static bool CanRunLengthEncode(Cell cell)
    {
        if (cell.Width != 1 || cell.GlyphId < 0) return false;
        var rune = new Rune(cell.GlyphId);
        return rune.IsAscii;
    }

    static void WriteRepeatPrevious(byte[] buffer, ref int pos, int count)
    {
        if (count <= 0) return;

        buffer[pos++] = 0x1b;
        buffer[pos++] = (byte)'[';

        if (!Utf8Formatter.TryFormat(count, buffer.AsSpan(pos), out var written))
            throw new InvalidOperationException("Failed to format repeat count.");

        pos += written;
        buffer[pos++] = (byte)'b';
    }

    bool WriteProgressSequence(byte[] buffer, ref int pos, RenderContext context)
    {
        if (!_useOscProgress) return false;

        if (TryFindProgress(context.UiContext.Root, out var percent))
        {
            if (_hasOscProgress && _lastOscProgress == percent) return false;
            Write(buffer, ref pos, TerminalProtocolSequences.Osc.SetProgressBytes(percent));
            _hasOscProgress = true;
            _lastOscProgress = percent;
            return true;
        }

        if (!_hasOscProgress) return false;

        Write(buffer, ref pos, TerminalProtocolSequences.Osc.ClearProgressBytes);
        _hasOscProgress = false;
        _lastOscProgress = -1;
        return true;
    }

    static bool TryFindProgress(IWidget root, out int percent)
    {
        var stack = new Stack<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            if (widget is ProgressBar progressBar)
            {
                var clamped = Math.Clamp(progressBar.Progress, 0d, 1d);
                percent = (int)Math.Round(clamped * 100d, MidpointRounding.AwayFromZero);
                return true;
            }

            var visitor = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref visitor);
        }

        percent = 0;
        return false;
    }

    readonly struct PushToStackVisitor(Stack<IWidget> stack) : IChildVisitor
    {
        public bool Visit(INode child)
        {
            if (child is IWidget widget)
                stack.Push(widget);
            return true;
        }
    }
}
