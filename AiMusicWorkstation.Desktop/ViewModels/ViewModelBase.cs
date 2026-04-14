using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AiMusicWorkstation.Desktop.ViewModels;

public class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void SetProperty<T>(
        ref T backingField,
        T value,
        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
            return;
        
        backingField = value;
        OnPropertyChanged(propertyName);
    }
    
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
