using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FastFileExplorer.Models;

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
                            IsDirectory = true
                        });
                    }

                    foreach (var file in Directory.GetFiles(path))
                    {
                        items.Add(new FileItem
                        {
                            Name = System.IO.Path.GetFileName(file),
                            Path = file,
                            IsDirectory = false
                        });
                    }
                }
            });

            return items;
        }
    }
}
