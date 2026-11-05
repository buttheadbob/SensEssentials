namespace SensEssentials.Quasar.Components;

/// <summary>
/// Shared by the editor's components: the status line at the bottom, with Undo after something was removed,
/// like the Torch tab's status bar.
/// </summary>
public sealed class EditorSession
{
    public event Action? Changed;

    public string? Message { get; private set; }

    public bool CanUndo => _undo != null;

    private Action? _undo;

    public void Edited()
    {
        Message = null;
        _undo = null;
        Changed?.Invoke();
    }

    public void Say(string message)
    {
        Message = message;
        _undo = null;
        Changed?.Invoke();
    }

    /// <summary>Reports a removal and offers to put it back.</summary>
    public void Removed(string message, Action undo)
    {
        Message = message;
        _undo = undo;
        Changed?.Invoke();
    }

    public void Undo()
    {
        var undo = _undo;
        _undo = null;
        if (undo == null)
            return;
        undo();
        Message = "Put back.";
        Changed?.Invoke();
    }

    public void Clear()
    {
        Message = null;
        _undo = null;
        Changed?.Invoke();
    }
}
