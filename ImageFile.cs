using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

public class ImageFile : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string FilePath { get; set; } = string.Empty;
    public ulong Hash { get; set; }
    public long FileSize { get; set; }
    public string GroupId { get; set; } = string.Empty;

    public int Width { get; set; }
    public int Height { get; set; }
    public long TotalPixels => (long)Width * Height;
    public string Dimensions => $"{Width} x {Height}";

    public string? FileName => Path.GetFileName(FilePath);
    public string DisplaySize => $"{(FileSize / 1024.0 / 1024.0):F2} MB";
    public string ImageSourceUri => $"file:///{FilePath?.Replace('\\', '/')}";

    private BitmapImage _thumbnail = new BitmapImage();
    public BitmapImage Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }
    
    private bool _isDuplicate;
    public bool IsDuplicate
    {
        get => _isDuplicate;
        set { _isDuplicate = value; OnPropertyChanged(); }
    }

    // WPF binding infrastructure
    public event PropertyChangedEventHandler? PropertyChanged;

    // The CallerMemberName attribute requires the using System.Runtime.CompilerServices; directive
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
