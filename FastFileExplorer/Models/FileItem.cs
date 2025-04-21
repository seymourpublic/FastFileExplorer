using Microsoft.UI.Xaml.Media;

namespace FastFileExplorer.Models
{
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public ImageSource? Icon { get; set; } 
    }
}
