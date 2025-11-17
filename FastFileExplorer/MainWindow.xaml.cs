using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using FastFileExplorer.Services;
using FastFileExplorer.Models;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI;

namespace FastFileExplorer
{
    public sealed partial class MainWindow : Window
    {
        private List<FileItem> allFiles = new List<FileItem>();
        private Stack<string> navigationHistory = new Stack<string>();
        private Stack<string> forwardHistory = new Stack<string>();
        private string currentFolder = "";
        private CancellationTokenSource? searchCancellationTokenSource;

        public MainWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            _ = LoadDrivesAsync();

            appWindow.Resize(new Windows.Graphics.SizeInt32(1000, 700));

            // Set title
            this.Title = "Fast File Explorer";
        }

        private async Task LoadDrivesAsync()
        {
            try
            {
                SetLoadingState(true);
                allFiles = await FileService.GetDrivesAsync();
                FileListView.ItemsSource = allFiles;
                BreadcrumbPanel.Children.Clear();
                SearchTextBox.Text = string.Empty;
                SearchPlaceholder.Text = "Navigate into a folder to enable search";
                currentFolder = string.Empty;
                forwardHistory.Clear();
                UpdateNavigationButtons();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Error Loading Drives", ex.Message);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCurrentView();
        }

        private async Task RefreshCurrentView()
        {
            try
            {
                SetLoadingState(true);

                if (string.IsNullOrWhiteSpace(currentFolder))
                {
                    await LoadDrivesAsync();
                }
                else
                {
                    allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                    FileListView.ItemsSource = allFiles;
                    SearchTextBox.Text = string.Empty;
                    SearchPlaceholder.Text = "Search files and folders...";
                    UpdateBreadcrumb(currentFolder);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Error Refreshing", ex.Message);
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Cancel previous search if still running
            searchCancellationTokenSource?.Cancel();
            searchCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = searchCancellationTokenSource.Token;

            var query = SearchTextBox.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(query))
            {
                try
                {
                    SetLoadingState(true);

                    if (string.IsNullOrWhiteSpace(currentFolder))
                    {
                        await LoadDrivesAsync();
                    }
                    else
                    {
                        allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                        FileListView.ItemsSource = allFiles;
                    }
                }
                finally
                {
                    SetLoadingState(false);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(currentFolder))
            {
                SearchPlaceholder.Text = "Navigate into a folder to enable search";
                return;
            }

            try
            {
                SetLoadingState(true);
                SearchPlaceholder.Text = "Searching...";

                // Add a small delay to avoid searching on every keystroke
                await Task.Delay(300, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                List<FileItem> allItems = await FileService.RecursiveScanAsync(currentFolder, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                Debug.WriteLine($"Recursive Items Found: {allItems.Count}");

                var filtered = allItems
                    .Where(f => f.Name.ToLower().Contains(query))
                    .OrderBy(f => !f.IsDirectory) // Folders first
                    .ThenBy(f => f.Name)
                    .ToList();

                FileListView.ItemsSource = filtered;
                SearchPlaceholder.Text = $"Found {filtered.Count} items";
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, this is expected
                Debug.WriteLine("Search cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Search error: {ex.Message}");
                SearchPlaceholder.Text = "Search error occurred";
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async void FileListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FileItem clickedItem)
            {
                if (clickedItem.IsDirectory)
                {
                    await NavigateToFolder(clickedItem.Path);
                }
                else
                {
                    await OpenFile(clickedItem.Path);
                }
            }
        }

        private async Task NavigateToFolder(string path)
        {
            try
            {
                SetLoadingState(true);

                navigationHistory.Push(currentFolder);
                forwardHistory.Clear(); // Clear forward history when navigating to a new location

                currentFolder = path;
                allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                FileListView.ItemsSource = allFiles;

                SearchTextBox.Text = string.Empty;
                SearchPlaceholder.Text = "Search files and folders...";
                UpdateBreadcrumb(currentFolder);
                UpdateNavigationButtons();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Navigation Error", $"Could not open folder.\n{ex.Message}");
            }
            finally
            {
                SetLoadingState(false);
            }
        }

        private async Task OpenFile(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("Error Opening File", $"Could not open file.\n{ex.Message}");
            }
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (navigationHistory.Count > 0)
            {
                try
                {
                    SetLoadingState(true);

                    forwardHistory.Push(currentFolder);
                    var previousPath = navigationHistory.Pop();

                    currentFolder = previousPath;
                    allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                    FileListView.ItemsSource = allFiles;

                    SearchTextBox.Text = string.Empty;
                    SearchPlaceholder.Text = string.IsNullOrWhiteSpace(currentFolder)
                        ? "Navigate into a folder to enable search"
                        : "Search files and folders...";
                    UpdateBreadcrumb(currentFolder);
                    UpdateNavigationButtons();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog("Navigation Error", ex.Message);
                }
                finally
                {
                    SetLoadingState(false);
                }
            }
        }

        private async void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (forwardHistory.Count > 0)
            {
                try
                {
                    SetLoadingState(true);

                    navigationHistory.Push(currentFolder);
                    var nextPath = forwardHistory.Pop();

                    currentFolder = nextPath;
                    allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                    FileListView.ItemsSource = allFiles;

                    SearchTextBox.Text = string.Empty;
                    SearchPlaceholder.Text = string.IsNullOrWhiteSpace(currentFolder)
                        ? "Navigate into a folder to enable search"
                        : "Search files and folders...";
                    UpdateBreadcrumb(currentFolder);
                    UpdateNavigationButtons();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog("Navigation Error", ex.Message);
                }
                finally
                {
                    SetLoadingState(false);
                }
            }
        }

        private async void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(currentFolder))
            {
                var parentPath = Path.GetDirectoryName(currentFolder);

                if (!string.IsNullOrWhiteSpace(parentPath))
                {
                    try
                    {
                        SetLoadingState(true);

                        navigationHistory.Push(currentFolder);
                        forwardHistory.Clear();
                        currentFolder = parentPath;

                        allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                        FileListView.ItemsSource = allFiles;

                        SearchTextBox.Text = string.Empty;
                        UpdateBreadcrumb(currentFolder);
                        UpdateNavigationButtons();
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorDialog("Navigation Error", ex.Message);
                    }
                    finally
                    {
                        SetLoadingState(false);
                    }
                }
                else
                {
                    // Navigate to drives view
                    await LoadDrivesAsync();
                }
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCurrentView();
        }

        private async void BreadcrumbButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                try
                {
                    SetLoadingState(true);

                    navigationHistory.Push(currentFolder);
                    forwardHistory.Clear();

                    currentFolder = path;
                    allFiles = await FileService.GetDirectoryContentsAsync(currentFolder);
                    FileListView.ItemsSource = allFiles;

                    SearchTextBox.Text = string.Empty;
                    SearchPlaceholder.Text = "Search files and folders...";
                    UpdateBreadcrumb(currentFolder);
                    UpdateNavigationButtons();
                }
                catch (Exception ex)
                {
                    await ShowErrorDialog("Navigation Error", ex.Message);
                }
                finally
                {
                    SetLoadingState(false);
                }
            }
        }

        private void UpdateBreadcrumb(string path)
        {
            BreadcrumbPanel.Children.Clear();

            if (string.IsNullOrWhiteSpace(path))
            {
                // Add "This PC" button when at root
                var thisPcButton = new Button
                {
                    Content = "This PC",
                    Margin = new Thickness(0, 0, 5, 0),
                    Tag = string.Empty,
                    MinWidth = 60,
                    Padding = new Thickness(8, 4, 8, 4),
                };
                thisPcButton.Click += async (s, e) => await LoadDrivesAsync();
                BreadcrumbPanel.Children.Add(thisPcButton);
                return;
            }

            // Add "This PC" button
            var rootButton = new Button
            {
                Content = "This PC",
                Margin = new Thickness(0, 0, 5, 0),
                Tag = string.Empty,
                MinWidth = 60,
                Padding = new Thickness(8, 4, 8, 4),
            };
            rootButton.Click += async (s, e) => await LoadDrivesAsync();
            BreadcrumbPanel.Children.Add(rootButton);

            BreadcrumbPanel.Children.Add(new TextBlock
            {
                Text = ">",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });

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
                    Padding = new Thickness(8, 4, 8, 4),
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

        private void UpdateNavigationButtons()
        {
            // Update button states based on navigation history
            // Note: You'll need to add x:Name attributes to the buttons in XAML
            // BackButton.IsEnabled = navigationHistory.Count > 0;
            // ForwardButton.IsEnabled = forwardHistory.Count > 0;
            // UpButton.IsEnabled = !string.IsNullOrWhiteSpace(currentFolder);
        }

        private void SetLoadingState(bool isLoading)
        {
            // Note: You'll need to add a ProgressRing or similar loading indicator in XAML
            // LoadingIndicator.IsActive = isLoading;
            // You can also disable navigation buttons while loading
        }

        private async Task ShowErrorDialog(string title, string message)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing dialog: {ex.Message}");
            }
        }
    }
}