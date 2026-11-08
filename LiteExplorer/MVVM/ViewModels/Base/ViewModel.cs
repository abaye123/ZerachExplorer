using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LiteExplorer.MVVM.ViewModels.Base;

internal abstract class ViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual bool SetValue<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (!Equals(field, value))
        {
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        return false;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
