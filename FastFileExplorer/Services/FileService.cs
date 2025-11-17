using FastFileExplorer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace FastFileExplorer.Services
{
    public static class FileService
    {
        private static readonly string[] ProtectedFolders = new[]
        {
            "$RECYCLE.BIN",
            "System Volume Information",
            "Recovery",
            "ProgramData\\Microsoft\\Windows\\Start Menu",
            "hiberfil.sys",
            "pagefile.sys"
        };

        public static async Task<List<FileItem>> GetDirectoryContentsAsync(string path)
        {
            var items = new List<FileItem>();

            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(path))
                    {
                        Debug.WriteLine($"Directory does not exist: {path}");
                        return;
                    }

                    // Get directories
                    try
                    {
                        var directories = Directory.GetDirectories(path);
                        foreach (var dir in directories)
                        {
                            try
                            {
                                var dirInfo = new DirectoryInfo(dir);

                                // Skip hidden and system folders
                                if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 ||
                                    (dirInfo.Attributes & FileAttributes.System) != 0)
                                {
                                    continue;
                                }

                                items.Add(new FileItem
                                {
                                    Name = dirInfo.Name,
                                    Path = dirInfo.FullName,
                                    IsDirectory = true,
                                    Icon = IconService.GetIconForFile(dir, true),
                                    Size = "",
                                    Type = "Folder",
                                    ModifiedDate = dirInfo.LastWriteTime.ToString("g")
                                });
                            }
                            catch (UnauthorizedAccessException)
                            {
                                Debug.WriteLine($"Access denied to directory: {dir}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error reading directory {dir}: {ex.Message}");
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Debug.WriteLine($"Access denied to path: {path}");
                    }

                    // Get files
                    try
                    {
                        var files = Directory.GetFiles(path);
                        foreach (var file in files)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);

                                // Skip hidden and system files
                                if ((fileInfo.Attributes & FileAttributes.Hidden) != 0 ||
                                    (fileInfo.Attributes & FileAttributes.System) != 0)
                                {
                                    continue;
                                }

                                items.Add(new FileItem
                                {
                                    Name = fileInfo.Name,
                                    Path = fileInfo.FullName,
                                    IsDirectory = false,
                                    Icon = IconService.GetIconForFile(file, false),
                                    Size = FormatFileSize(fileInfo.Length),
                                    Type = string.IsNullOrEmpty(fileInfo.Extension)
                                        ? "File"
                                        : fileInfo.Extension.TrimStart('.').ToUpper(),
                                    ModifiedDate = fileInfo.LastWriteTime.ToString("g")
                                });
                            }
                            catch (UnauthorizedAccessException)
                            {
                                Debug.WriteLine($"Access denied to file: {file}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error reading file {file}: {ex.Message}");
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Debug.WriteLine($"Access denied to files in: {path}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting directory contents for {path}: {ex.Message}");
                }
            });

            return items;
        }

        public static async Task<List<FileItem>> GetDrivesAsync()
        {
            var drives = new List<FileItem>();

            await Task.Run(() =>
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.IsReady)
                        {
                            var driveName = drive.Name;
                            if (!string.IsNullOrEmpty(drive.VolumeLabel))
                            {
                                driveName = $"{drive.Name} ({drive.VolumeLabel})";
                            }

                            drives.Add(new FileItem
                            {
                                Name = driveName,
                                Path = drive.RootDirectory.FullName,
                                IsDirectory = true,
                                Icon = IconService.GetIconForFile(drive.RootDirectory.FullName, true),
                                Type = drive.DriveType.ToString(),
                                Size = $"{FormatFileSize(drive.AvailableFreeSpace)} free of {FormatFileSize(drive.TotalSize)}",
                                ModifiedDate = ""
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error reading drive {drive.Name}: {ex.Message}");
                    }
                }
            });

            return drives;
        }

        public static async Task<List<FileItem>> RecursiveScanAsync(string path, CancellationToken cancellationToken = default)
        {
            var items = new List<FileItem>();

            await Task.Run(() =>
            {
                RecursiveScanInternal(path, items, cancellationToken);
            }, cancellationToken);

            return items;
        }

        private static void RecursiveScanInternal(string path, List<FileItem> items, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                if (!Directory.Exists(path))
                    return;

                // Check if this is a protected folder
                if (IsProtectedFolder(path))
                {
                    Debug.WriteLine($"Skipping protected folder: {path}");
                    return;
                }

                var dirInfo = new DirectoryInfo(path);

                // Skip hidden and system folders
                if ((dirInfo.Attributes & FileAttributes.Hidden) != 0 ||
                    (dirInfo.Attributes & FileAttributes.System) != 0)
                {
                    return;
                }

                // Get directories
                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        try
                        {
                            var subDirInfo = new DirectoryInfo(dir);

                            // Skip hidden and system folders
                            if ((subDirInfo.Attributes & FileAttributes.Hidden) != 0 ||
                                (subDirInfo.Attributes & FileAttributes.System) != 0)
                            {
                                continue;
                            }

                            items.Add(new FileItem
                            {
                                Name = subDirInfo.Name,
                                Path = subDirInfo.FullName,
                                IsDirectory = true,
                                Icon = IconService.GetIconForFile(dir, true),
                                Type = "Folder",
                                Size = "",
                                ModifiedDate = subDirInfo.LastWriteTime.ToString("g")
                            });

                            // Recursively scan subdirectory
                            RecursiveScanInternal(dir, items, cancellationToken);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Debug.WriteLine($"Access denied: {dir}");
                        }
                        catch (PathTooLongException)
                        {
                            Debug.WriteLine($"Path too long: {dir}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error scanning directory {dir}: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine($"Access denied to directories in: {path}");
                }

                // Get files
                try
                {
                    foreach (var file in Directory.GetFiles(path))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        try
                        {
                            var fileInfo = new FileInfo(file);

                            // Skip hidden and system files
                            if ((fileInfo.Attributes & FileAttributes.Hidden) != 0 ||
                                (fileInfo.Attributes & FileAttributes.System) != 0)
                            {
                                continue;
                            }

                            items.Add(new FileItem
                            {
                                Name = fileInfo.Name,
                                Path = fileInfo.FullName,
                                IsDirectory = false,
                                Icon = IconService.GetIconForFile(file, false),
                                Type = string.IsNullOrEmpty(fileInfo.Extension)
                                    ? "File"
                                    : fileInfo.Extension.TrimStart('.').ToUpper(),
                                Size = FormatFileSize(fileInfo.Length),
                                ModifiedDate = fileInfo.LastWriteTime.ToString("g")
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Debug.WriteLine($"Access denied: {file}");
                        }
                        catch (PathTooLongException)
                        {
                            Debug.WriteLine($"Path too long: {file}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error reading file {file}: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine($"Access denied to files in: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Recursive scan error for {path}: {ex.Message}");
            }
        }

        private static bool IsProtectedFolder(string path)
        {
            return ProtectedFolders.Any(pf =>
                path.IndexOf(pf, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}