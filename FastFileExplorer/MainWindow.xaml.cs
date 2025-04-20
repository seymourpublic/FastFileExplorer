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

namespace FastFileExplorer
{
    public sealed partial class MainWindow : Window
    {
        private List<FileItem> allFiles = new List<FileItem>();

        public MainWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32(900, 600));
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var path = PathTextBox.Text;

            if (!string.IsNullOrWhiteSpace(path))
            {
                allFiles = await FileService.GetDirectoryContentsAsync(path);
                FileListView.ItemsSource = allFiles;
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
    }
}
