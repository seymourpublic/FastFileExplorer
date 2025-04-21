using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using FastFileExplorer.Services;
using FastFileExplorer.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FastFileExplorer
{
    public sealed partial class MainWindow : Window
    {
        private List<FileItem> allFiles = new List<FileItem>();
        private Stack<string> navigationHistory = new Stack<string>();
        private string currentFolder = "";

        public MainWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            _ = LoadDrivesAsync();

            appWindow.Resize(new Windows.Graphics.SizeInt32(900, 600));
        }

        private async Task LoadDrivesAsync()
        {
            allFiles = await FileService.GetDrivesAsync();
            FileListView.ItemsSource = allFiles;
            BreadcrumbPanel.Children.Clear(); // No breadcrumb when listing drives
            SearchTextBox.Text = string.Empty;
            currentFolder = string.Empty;
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(currentFolder))
            {
                await LoadDrivesAsync();
            }
            else
            {
                allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                FileListView.ItemsSource = allFiles;
                SearchTextBox.Text = string.Empty;
                UpdateBreadcrumb(currentFolder);
            }
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(query))
            {
                // Reload drives or current folder
                if (string.IsNullOrWhiteSpace(currentFolder))
                    await LoadDrivesAsync();
                else
                    allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);

                FileListView.ItemsSource = allFiles;
            }
            else
            {
                List<FileItem> allItems = new List<FileItem>();

                if (string.IsNullOrWhiteSpace(currentFolder))
                {
                    // Search across all drives
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (drive.IsReady)
                        {
                            var driveItems = await FileService.GetDirectoryContentsRecursiveAsync(drive.RootDirectory.FullName);
                            allItems.AddRange(driveItems);
                        }
                    }
                }
                else
                {
                    // Search inside current folder and subfolders
                    allItems = await FileService.GetDirectoryContentsRecursiveAsync(currentFolder);
                }

                var filtered = allItems.Where(f => f.Name.ToLower().Contains(query)).ToList();
                FileListView.ItemsSource = filtered;
            }
        }

        private async void FileListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FileItem clickedItem)
            {
                if (clickedItem.IsDirectory)
                {
                    navigationHistory.Push(currentFolder);

                    currentFolder = clickedItem.Path;
                    allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                    FileListView.ItemsSource = allFiles;

                    SearchTextBox.Text = string.Empty;
                    UpdateBreadcrumb(currentFolder);
                }
                else
                {
                    // ?? OPEN FILE WITH DEFAULT APP
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = clickedItem.Path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        await new ContentDialog
                        {
                            Title = "Error",
                            Content = $"Could not open file.\n{ex.Message}",
                            CloseButtonText = "OK"
                        }.ShowAsync();
                    }
                }
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (navigationHistory.Count > 0)
            {
                var previousPath = navigationHistory.Pop();

                currentFolder = previousPath;
                allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                FileListView.ItemsSource = allFiles;

                SearchTextBox.Text = string.Empty;
                UpdateBreadcrumb(currentFolder);
            }
        }

        private async void BreadcrumbButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                navigationHistory.Push(currentFolder);

                currentFolder = path;
                allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                FileListView.ItemsSource = allFiles;

                SearchTextBox.Text = string.Empty;
                UpdateBreadcrumb(currentFolder);
            }
        }

        private void UpdateBreadcrumb(string path)
        {
            BreadcrumbPanel.Children.Clear();

            if (string.IsNullOrWhiteSpace(path)) return;

            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            .Where(p => !string.IsNullOrEmpty(p))
                            .ToArray();

            string currentPath = "";

            for (int i = 0; i < parts.Length; i++)
            {
                if (i == 0 && path.Contains(":"))
                    currentPath = parts[i];
                else
                    currentPath = Path.Combine(currentPath, parts[i]);

                var button = new Button
                {
                    Content = parts[i],
                    Margin = new Thickness(0, 0, 5, 0),
                    Tag = currentPath,
                    MinWidth = 40,
                    Padding = new Thickness(5, 0, 5, 0),
                };
                button.Click += BreadcrumbButton_Click;

                BreadcrumbPanel.Children.Add(button);

                if (i < parts.Length - 1)
                {
                    BreadcrumbPanel.Children.Add(new TextBlock
                    {
                        Text = ">",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 5, 0)
                    });
                }
            }
        }
    }
}
