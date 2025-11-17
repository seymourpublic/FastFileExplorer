using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace FastFileExplorer.Services
{
    public static class IconService
    {
        private static readonly Dictionary<string, BitmapImage> IconCache = new Dictionary<string, BitmapImage>();
        private static readonly object CacheLock = new object();
        private const int MaxCacheSize = 1000;

        public static BitmapImage? GetIconForFile(string path, bool isDirectory)
        {
            try
            {
                // Create cache key based on extension for files, or "folder" for directories
                string cacheKey;
                if (isDirectory)
                {
                    cacheKey = "folder";
                }
                else
                {
                    var extension = Path.GetExtension(path)?.ToLowerInvariant() ?? "noext";
                    cacheKey = $"file_{extension}";
                }

                // Check cache first
                lock (CacheLock)
                {
                    if (IconCache.TryGetValue(cacheKey, out var cachedIcon))
                    {
                        return cachedIcon;
                    }
                }

                // Get icon from system
                BitmapImage? icon;
                if (isDirectory)
                {
                    icon = GetSystemIcon(path, Shell32.SHGFI.SHGFI_ICON | Shell32.SHGFI.SHGFI_LARGEICON);
                }
                else
                {
                    icon = GetSystemIcon(path, Shell32.SHGFI.SHGFI_ICON | Shell32.SHGFI.SHGFI_SMALLICON | Shell32.SHGFI.SHGFI_USEFILEATTRIBUTES);
                }

                // Cache the icon
                if (icon != null)
                {
                    lock (CacheLock)
                    {
                        // Prevent cache from growing too large
                        if (IconCache.Count >= MaxCacheSize)
                        {
                            IconCache.Clear();
                        }

                        if (!IconCache.ContainsKey(cacheKey))
                        {
                            IconCache[cacheKey] = icon;
                        }
                    }
                }

                return icon;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting icon for {path}: {ex.Message}");
                return null;
            }
        }

        private static BitmapImage? GetSystemIcon(string path, Shell32.SHGFI flags)
        {
            try
            {
                var shinfo = new Shell32.SHFILEINFO();
                var result = Shell32.SHGetFileInfo(
                    path,
                    (System.IO.FileAttributes)0x80, // FILE_ATTRIBUTE_NORMAL
                    ref shinfo,
                    Marshal.SizeOf(shinfo),
                    flags);

                if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                {
                    Debug.WriteLine($"Failed to get icon for: {path}");
                    return null;
                }

                try
                {
                    using (var icon = System.Drawing.Icon.FromHandle((IntPtr)shinfo.hIcon))
                    {
                        using (var bitmap = icon.ToBitmap())
                        {
                            using (var stream = new InMemoryRandomAccessStream())
                            {
                                bitmap.Save(stream.AsStreamForWrite(), System.Drawing.Imaging.ImageFormat.Png);
                                stream.Seek(0);

                                var img = new BitmapImage();
                                img.SetSourceAsync(stream).AsTask().Wait();
                                return img;
                            }
                        }
                    }
                }
                finally
                {
                    // Clean up the icon handle
                    if (shinfo.hIcon != IntPtr.Zero)
                    {
                        User32.DestroyIcon(shinfo.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in GetSystemIcon: {ex.Message}");
                return null;
            }
        }

        public static void ClearCache()
        {
            lock (CacheLock)
            {
                IconCache.Clear();
            }
        }
    }
}