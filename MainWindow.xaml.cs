using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;


namespace ImageDeduplicator
{
    public partial class MainWindow : Window
    {
        // The collection bound to the ListView in the XAML
        private static readonly object _collectionLock = new object();
        public ObservableCollection<ImageFile> SimilarImages { get; set; } = new ObservableCollection<ImageFile>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            // Initialize the grouping description once at startup
            BindingOperations.EnableCollectionSynchronization(SimilarImages, _collectionLock);
            var view = CollectionViewSource.GetDefaultView(SimilarImages);
            view.GroupDescriptions.Add(new PropertyGroupDescription("GroupId"));
        }

        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".pbm", ".tga", ".tiff", ".tif"
        };

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var windowHelper = new WindowInteropHelper(this);

            // This tells Windows to apply the blur effect to our specific window handle
            SetWindowBlur(windowHelper.Handle);
        }

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        internal void SetWindowBlur(IntPtr hwnd)
        {
            var accent = new AccentPolicy { AccentState = 3 }; // 3 = AccentEnableBlurBehind
            var accentStructSize = Marshal.SizeOf(accent);
            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = 19, // WCA_ACCENT_POLICY
                SizeOfData = accentStructSize,
                Data = accentPtr
            };

            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }

        // Helper structs for the API call
        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy { public int AccentState; public int AccentFlags; public int GradientColor; public int AnimationId; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

        // This initializes the drag behavior for our custom window style
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the left button was pressed so we don't move the window 
            // when you're just trying to right-click something!
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();

        // --- Folder Selection ---
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.Title = "Select Folder";
            dialog.IsFolderPicker = true;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                dialog.IsFolderPicker = true;
                string? folderPath = dialog.FileName;
                if (folderPath != null)
                {
                    SearchPathTextBox.Text = folderPath;
                    Search_Click(sender, null);
                }
            }
        }

        private void ThresholdSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // We call the existing Search_Click logic to initiate the re-scan.
            // This is the cleanest way because Search_Click already initializes 
            // the threshold and path variables for us.
            Search_Click(sender, null);
        }

        // --- Search Logic ---
        private async void Search_Click(object sender, RoutedEventArgs? e)
        {
            // ... (Search setup and error checking remains the same)
            string rootPath = SearchPathTextBox.Text.Trim();
            int threshold = (int)ThresholdSlider.Value;

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                MessageBox.Show("The path cannot be empty, Master.", "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(rootPath))
            {
                MessageBox.Show($"I cannot find the path: {rootPath}\nPlease check your spelling.", "Path Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SimilarImages.Clear();
            SearchButton.IsEnabled = false;
            ThresholdSlider.IsEnabled = false;
            SelectNoneButton.IsEnabled = false;
            SelectDupButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            StatusText.Text = "Status: Scanning and Hashing files...";
            List<ImageFile> results = new();

            try
            {
                // 1. Pass the dynamic threshold value to the worker method ***
                results = await Task.Run(() => FindDuplicates(rootPath, threshold));
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("I don't have permission to access some folders in that path, Master.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred: {ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SearchButton.IsEnabled = true;
                ThresholdSlider.IsEnabled = true;
                SelectNoneButton.IsEnabled = true;
                SelectDupButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
            }

            lock (_collectionLock)
            {
                foreach (var item in results)
                {
                    SimilarImages.Add(item);
                }
            }
            //Dispatcher.Invoke(() => CollectionViewSource.GetDefaultView(SimilarImages).Refresh());
            StatusText.Text = $"Status: Search complete. Found {SimilarImages.Count} similar files.";
        }

        private List<ImageFile> FindDuplicates(string rootPath, int threshold)
        {
            List<ImageFile> foundDuplicates = new List<ImageFile>();

            var allFiles = Directory.EnumerateFiles(rootPath, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file)))
                .ToList();

            var imageList = allFiles.Select(file =>
            {
                //Dispatcher.Invoke(() => StatusText.Text = $"Status: Hashing {Path.GetFileName(file)}...");
                var metadata = ImageHasher.GetImageMetadata(file);

                return new ImageFile
                {
                    FilePath = file,
                    FileSize = new FileInfo(file).Length,
                    Hash = metadata.Hash,
                    Width = metadata.Width,
                    Height = metadata.Height
                };
            })
            .Where(f => f.Hash != 0)
            .ToList();

            var alreadyGrouped = new HashSet<ImageFile>();
            int groupIdCounter = 1;

            for (int i = 0; i < imageList.Count; i++)
            {
                var file1 = imageList[i];
                if (alreadyGrouped.Contains(file1)) continue;

                List<ImageFile> currentGroup = new List<ImageFile> { file1 };
                alreadyGrouped.Add(file1);

                for (int j = i + 1; j < imageList.Count; j++)
                {
                    var file2 = imageList[j];
                    if (alreadyGrouped.Contains(file2)) continue;

                    int distance = ImageHasher.CalculateHammingDistance(file1.Hash, file2.Hash);

                    if (distance <= threshold)
                    {
                        currentGroup.Add(file2);
                        alreadyGrouped.Add(file2);
                    }
                }

                if (currentGroup.Count > 1)
                {
                    // --- MASTER'S NEW SORTING LOGIC ---
                    // 1. Primary: Total Pixels (Width * Height) 2. Secondary: File Size
                    var sortedGroup = currentGroup
                        .OrderByDescending(f => f.TotalPixels)
                        .ThenByDescending(f => f.FileSize)
                        .ToList();

                    var original = sortedGroup.First();
                    original.Thumbnail = LoadImageWithoutLocking(original.FilePath);
                    original.IsDuplicate = false; // Initializing as the master copy
                    string newGroupId = $"Group {groupIdCounter++}";
                    original.GroupId = newGroupId;
                    original.IsSelected = false; // Original is safe!
                    foundDuplicates.Add(original);
                    //Dispatcher.Invoke(() => SimilarImages.Add(original));

                    foreach (var duplicate in sortedGroup.Skip(1))
                    {
                        duplicate.Thumbnail = LoadImageWithoutLocking(duplicate.FilePath);
                        duplicate.GroupId = newGroupId; // + " (D)";
                        duplicate.IsDuplicate = true;
                        duplicate.IsSelected = true;
                        foundDuplicates.Add(duplicate);
                        //Dispatcher.Invoke(() => SimilarImages.Add(duplicate));
                    }
                }
                
            }
            return foundDuplicates;
        }
        
        // --- Selection Controls ---
        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in SimilarImages)
            {
                item.IsSelected = false;
            }
        }
        // Select only Duplicates
        private void SelectDuplicatesOnly_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in SimilarImages)
            {
                item.IsSelected = false;
            }

            // select only the ones marked as duplicates
            var duplicates = SimilarImages.Where(f => f.IsDuplicate);
            foreach (var item in duplicates)
            {
                item.IsSelected = true;
            }
        }
        // --- Deletion Logic (NOW USING FileUtility) ---
        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var filesToDelete = SimilarImages.Where(f => f.IsSelected).ToList();

            if (!filesToDelete.Any())
            {
                MessageBox.Show("No files are currently selected for deletion.", "Deletion", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete {filesToDelete.Count} files to Recycle Bin?", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                int deletedCount = 0;
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        // Call the P/Invoke utility
                        bool success = FileUtility.MoveToRecycleBin(file.FilePath);

                        if (success)
                        {
                            // Remove from the display list only if deletion was successful
                            SimilarImages.Remove(file);
                            deletedCount++;
                        }
                        else
                        {
                            // Handle failure of the API call
                            MessageBox.Show($"Could not delete file: {file.FileName}. The file might be in use or access was denied.", "Deletion Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"An unexpected error occurred while deleting {file.FileName}.\nError: {ex.Message}", "Deletion Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                StatusText.Text = $"Status: Successfully moved {deletedCount} files to the Recycle Bin.";
            }
        }
        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Initialize double-click detection (Left button, ClickCount 2)
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                // Use the explicit control type from System.Windows.Controls
                if (sender is System.Windows.Controls.Image img && img.DataContext is ImageFile clickedFile)
                {
                    try
                    {
                        if (File.Exists(clickedFile.FilePath))
                        {
                            // Initialize the process to open the file in the default viewer
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = clickedFile.FilePath,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Could not open file: {ex.Message}", "System Error");
                    }
                }
            }
        }

        private BitmapImage LoadImageWithoutLocking(string path)
        {
            // Initialize a bitmap that loads into memory and releases the file
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // This is the critical initialization
            bitmap.EndInit();
            bitmap.Freeze(); // Makes it safe for the UI thread
            return bitmap;
        }
    }
}
