using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

public class ImageFile : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string? FilePath { get; set; }
    public ulong Hash { get; set; }
    public long FileSize { get; set; }
    public string? GroupId { get; set; }

    public string? FileName => Path.GetFileName(FilePath);
    public string DisplaySize => $"{(FileSize / 1024.0 / 1024.0):F2} MB";
    public string ImageSourceUri => $"file:///{FilePath?.Replace('\\', '/')}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    // WPF binding infrastructure
    public event PropertyChangedEventHandler? PropertyChanged;

    // The CallerMemberName attribute requires the using System.Runtime.CompilerServices; directive
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
