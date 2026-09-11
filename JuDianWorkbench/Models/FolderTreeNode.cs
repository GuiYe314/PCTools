using System.Collections.ObjectModel;

namespace JuDianWorkbench.Models;

public sealed class FolderTreeNode
{
    private const int MaxChildren = 500;
    private bool _loaded;
    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public bool IsPlaceholder { get; }
    public string Icon => IsPlaceholder ? "…" : IsDirectory ? "📁" : "📄";
    public ObservableCollection<FolderTreeNode> Children { get; } = [];

    public FolderTreeNode(string path, bool? isDirectory = null)
    {
        FullPath = path;
        IsDirectory = isDirectory ?? Directory.Exists(path);
        Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(Name)) Name = path;
        if (IsDirectory && HasChildren(path)) Children.Add(new FolderTreeNode("正在读取…", 0));
    }

    private FolderTreeNode(string label, byte placeholderMarker)
    {
        Name = label;
        FullPath = string.Empty;
        _loaded = true;
        IsPlaceholder = true;
    }

    public void LoadChildren()
    {
        if (_loaded || string.IsNullOrWhiteSpace(FullPath)) return;
        _loaded = true;
        Children.Clear();
        try
        {
            var count = 0;
            foreach (var directory in Directory.EnumerateDirectories(FullPath).OrderBy(x => x))
            {
                if (count++ >= MaxChildren) break;
                Children.Add(new FolderTreeNode(directory, true));
            }
            foreach (var file in Directory.EnumerateFiles(FullPath).OrderBy(x => x))
            {
                if (count++ >= MaxChildren) break;
                Children.Add(new FolderTreeNode(file, false));
            }
            if (count > MaxChildren) Children.Add(new FolderTreeNode($"仅显示前 {MaxChildren} 项", 0));
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    private static bool HasChildren(string path)
    {
        try { return Directory.EnumerateFileSystemEntries(path).Any(); }
        catch { return false; }
    }
}
