using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Storage.Streams;
using FastFileExplorer.Models;

namespace FastFileExplorer.Services
{
    public static class IconService
    {
        public static BitmapImage GetIconForFile(string path, bool isDirectory)
        {
            try
            {
                if (isDirectory)
                {
                    // Use a default folder image from Windows shell
                    var uri = new Uri("ms-appx:///Assets/folder-icon.png");
                    return new BitmapImage(uri);
                }
                else
                {
                    // For now, just a default file image
                    var uri = new Uri("ms-appx:///Assets/file-icon.png");
                    return new BitmapImage(uri);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
