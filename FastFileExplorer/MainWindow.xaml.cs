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
            currentFolder = "C:\\";

            appWindow.Resize(new Windows.Graphics.SizeInt32(900, 600));

        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(currentFolder))
            {
                allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                FileListView.ItemsSource = allFiles;
                SearchTextBox.Text = string.Empty;

                UpdateBreadcrumb(currentFolder);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchTextBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(query))
            {
                FileListView.ItemsSource = allFiles;
            }
            else
            {
                var filtered = allFiles.Where(f => f.Name.ToLower().Contains(query)).ToList();
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
                    // It's a file ? maybe later we can open it with default app
                    // For now, do nothing
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
