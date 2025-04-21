using FastFileExplorer.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FastFileExplorer.Services
{
    public static class FileService
    {
        public static async Task<List<FileItem>> GetDirectoryContentsAsync(string path)
        {
            var items = new List<FileItem>();

            await Task.Run(() =>
            {
                if (Directory.Exists(path))
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        items.Add(new FileItem
                        {
                            Name = System.IO.Path.GetFileName(dir),
                            Path = dir,
                            IsDirectory = true,
                            Icon = IconService.GetIconForFile(dir, true)
                        });
                    }

                    foreach (var file in Directory.GetFiles(path))
                    {
                        items.Add(new FileItem
                        {
                            Name = System.IO.Path.GetFileName(file),
                            Path = file,
                            IsDirectory = false,
                            Icon = IconService.GetIconForFile(file, false)
                        });
                    }
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
                    if (drive.IsReady)
                    {
                        drives.Add(new FileItem
                        {
                            Name = drive.Name,
                            Path = drive.RootDirectory.FullName,
                            IsDirectory = true,
                            Icon = IconService.GetIconForFile(drive.RootDirectory.FullName, true)
                        });
                    }
                }
            });

            return drives;
        }
    }
}
