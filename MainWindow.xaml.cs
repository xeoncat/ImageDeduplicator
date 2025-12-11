//using System;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
//using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Data;
//using System.Windows.Documents;
//using System.Windows.Input;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Navigation;
//using System.Windows.Shapes;

namespace ImageDeduplicator
{
    public partial class MainWindow : Window
    {
        // The collection bound to the ListView in the XAML
        public ObservableCollection<ImageFile> SimilarImages { get; set; } = new ObservableCollection<ImageFile>();

        //private const int SimilarityThreshold = 1; // Hamming distance threshold for similarity

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }
        // --- Folder Selection ---
        private void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            // Use the native WPF OpenFileDialog to select a file within the desired folder.
            var dialog = new CommonOpenFileDialog();
            dialog.Title = "Select Folder";
            dialog.IsFolderPicker = true;

            // Set a filter for common image extensions for clarity
            //dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All Files (*.*)|*.*";

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // We don't want the file, we want the folder it's in.
                // Path.GetDirectoryName is the hot trick here!
                dialog.IsFolderPicker = true;
                string folderPath = dialog.FileName;
                SearchPathTextBox.Text = folderPath;
                Search_Click(sender, null);
            }
        }
        // --- NEW: Slider Release Handler ---
        private void ThresholdSlider_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // We call the existing Search_Click logic to initiate the search.
            // We pass the slider as the sender and null for the RoutedEventArgs, 
            // since the Search_Click method doesn't strictly rely on the event data.
            Search_Click(sender, null);
        }
        // --- Search Logic ---
        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            // ... (Search setup and error checking remains the same)
            string rootPath = SearchPathTextBox.Text;
            if (!Directory.Exists(rootPath))
            {
                MessageBox.Show("Please select a valid search folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // *** NEW: Get and validate the Hamming Distance threshold ***
            int threshold = (int)ThresholdSlider.Value;
            // Clear previous results and disable button
            SimilarImages.Clear();
            SearchButton.IsEnabled = false;
            StatusText.Text = "Status: Scanning and Hashing files...";
            // *** Pass the dynamic threshold value to the worker method ***
            await Task.Run(() => FindDuplicates(rootPath, threshold));

            SearchButton.IsEnabled = true;
            StatusText.Text = $"Status: Search complete. Found {SimilarImages.Count} similar files.";
        }

        private void FindDuplicates(string rootPath, int threshold)
        {
            // ... (Core hashing and grouping logic remains the same)
            var allFiles = Directory.EnumerateFiles(rootPath, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".jpg") || s.EndsWith(".jpeg") || s.EndsWith(".png") || s.EndsWith(".gif") || s.EndsWith(".bmp"))
                .ToList();

            var imageList = allFiles.Select(file =>
            {
                Dispatcher.Invoke(() => StatusText.Text = $"Status: Hashing {Path.GetFileName(file)}...");
                return new ImageFile
                {
                    FilePath = file,
                    FileSize = new FileInfo(file).Length,
                    Hash = ImageHasher.ComputeAverageHash(file)
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
                    var sortedGroup = currentGroup.OrderByDescending(f => f.FileSize).ToList();
                    var original = sortedGroup.First();

                    string newGroupId = $"Group {groupIdCounter++}";

                    original.GroupId = newGroupId;
                    Dispatcher.Invoke(() => SimilarImages.Add(original));

                    foreach (var duplicate in sortedGroup.Skip(1))
                    {
                        duplicate.GroupId = newGroupId + " (Duplicate)";
                        Dispatcher.Invoke(() => SimilarImages.Add(duplicate));
                    }
                }
            }
        }
        // --- Selection Controls ---
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in SimilarImages)
            {
                item.IsSelected = true;
            }
        }

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
            // First, clear all selections
            foreach (var item in SimilarImages)
            {
                item.IsSelected = false;
            }

            // Then, select only the ones marked as duplicates (the smaller file size ones)
            foreach (var item in SimilarImages.Where(f => f.GroupId.Contains("(Duplicate)")))
            {
                item.IsSelected = true;
            }
        }
        // --- Deletion Logic (NOW USING RecycleBinUtility) ---
        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var filesToDelete = SimilarImages.Where(f => f.IsSelected).ToList();

            if (!filesToDelete.Any())
            {
                MessageBox.Show("No files are currently selected for deletion.", "Deletion", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete {filesToDelete.Count} selected files to the Recycle Bin, Master?", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                int deletedCount = 0;
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        // Call the P/Invoke utility instead of the VisualBasic library
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
    }
}
