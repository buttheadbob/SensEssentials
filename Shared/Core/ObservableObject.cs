using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SensEssentials;

/// <summary>
/// Base class for config items. Raises PropertyChanged so the Torch UI updates and the config saves itself.
/// Same members as Torch's ViewModel, which the shared code cannot reference.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    /// <summary>
    /// Assigns the value and raises PropertyChanged when it changed.
    /// </summary>
    protected virtual void SetValue<T>(ref T field, T value, [CallerMemberName] string propName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propName);
    }
}
