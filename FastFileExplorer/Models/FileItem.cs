namespace FastFileExplorer.Models
{
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public Microsoft.UI.Xaml.Media.ImageSource? Icon { get; set; }
    }
}
