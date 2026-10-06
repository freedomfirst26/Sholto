using System.ComponentModel;
using Avalonia.Input;
using Sholto.Interface.MainUI.Controls.Modal;

namespace Sholto.Interface.MainUI.Tests.Modal;

/// <summary>A modal content a test sets by hand; it counts what was called and raises PropertyChanged on demand.</summary>
internal sealed class FakeModalContent : IModalContent
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsOpen { get; set; } = true;
    public ModalScrimClick ScrimClick { get; set; } = ModalScrimClick.Dismisses;
    public bool CapturesText { get; set; }
    public string Eyebrow { get; set; } = "EYEBROW";
    public ModalTone Tone { get; set; } = ModalTone.Accent;
    public string Title { get; set; } = "Title";
    public string? Subtitle { get; set; } = "Subtitle";
    public string KeyHint { get; set; } = "Esc closes";
    public ModalWidth Width { get; set; } = ModalWidth.Regular;
    public ModalButtons Buttons { get; set; } = new("Close", null, null);
    public bool CanGoBack { get; set; } = true;
    public bool CanConfirm { get; set; } = true;

    /// <summary>Keys <see cref="HandleKey"/> reports as handled.</summary>
    public HashSet<Key> HandledKeys { get; } = [];

    public int DismissCalls { get; private set; }
    public int BackCalls { get; private set; }
    public int ConfirmCalls { get; private set; }
    public List<Key> KeysSeen { get; } = [];

    public void Dismiss() => DismissCalls++;
    public void Back() => BackCalls++;
    public void Confirm() => ConfirmCalls++;

    public bool HandleKey(Key key, KeyModifiers modifiers)
    {
        KeysSeen.Add(key);
        return HandledKeys.Contains(key);
    }

    public void Raise(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
