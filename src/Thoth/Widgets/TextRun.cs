namespace Thoth.Widgets;

public readonly record struct TextRun(string Text, StyleId? StyleId = null, LinkId? LinkId = null);