using System.Collections.Generic;
using Thoth.Widgets;

namespace Thoth.Modal;

public readonly record struct ModalDialogButton(string Id, string Label, bool IsDefault = false);

public readonly record struct ModalDialogChoice(string Id, string Label, bool IsChecked = false);

public readonly record struct OpenModalDialogCommand(string title,
                                                     bool mandatory,
                                                     IReadOnlyList<ModalDialogButton> buttons,
                                                     IReadOnlyList<IWidget>? content = null)
{
    public string Title { get; } = title ?? string.Empty;

    public bool Mandatory { get; } = mandatory;

    public IReadOnlyList<ModalDialogButton> Buttons { get; } = buttons ?? [];

    public IReadOnlyList<IWidget> Content { get; } = content ?? [];
}

public readonly record struct OnModalDialogClosed(string? ButtonId,
                                                  IReadOnlyList<string> SelectedChoiceIds,
                                                  bool Dismissed);
