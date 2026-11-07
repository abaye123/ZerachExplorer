using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LiteExplorer.MVVM.ViewModels.Base;

/// <summary>
/// Base viewmodel inheriting from CommunityToolkit.Mvvm ObservableObject.
/// </summary>
internal abstract class ViewModel : ObservableObject
{
    protected virtual bool SetValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        return SetProperty(ref field, value, propertyName);
    }
}
