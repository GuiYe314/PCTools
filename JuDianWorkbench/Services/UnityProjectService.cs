using System.Diagnostics;
using JuDianWorkbench.Models;

namespace JuDianWorkbench.Services;

public sealed class UnityProjectService
{
    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "Library", "Temp", "Logs", "obj", "Build", "Builds", "UserSettings"
    };

    public IReadOnlyList<UnityProjectInfo> FindProjects(string managedPath, int maxDepth = 4)
    {
        if (string.IsNullOrWhiteSpace(managedPath) || !Directory.Exists(managedPath)) return [];

        var projects = new List<UnityProjectInfo>();
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((Path.GetFullPath(managedPath), 0));

        while (pending.Count > 0 && projects.Count < 20)
        {
            var current = pending.Dequeue();
            if (IsUnityProjectRoot(current.Path))
            {
                var version = ReadEditorVersion(current.Path);
                projects.Add(new UnityProjectInfo
                {
                    ProjectName = Path.GetFileName(current.Path.TrimEnd(Path.DirectorySeparatorChar)),
                    ProjectPath = current.Path,
                    EditorVersion = version,
                    EditorPath = FindMatchingEditor(version)
                });
                continue;
            }

            if (current.Depth >= maxDepth) continue;
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(current.Path))
                {
                    var name = Path.GetFileName(directory);
                    if (!SkippedDirectoryNames.Contains(name)) pending.Enqueue((directory, current.Depth + 1));
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return projects.OrderBy(x => x.ProjectName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public int LaunchProject(UnityProjectInfo project)
    {
        if (!IsUnityProjectRoot(project.ProjectPath)) throw new DirectoryNotFoundException("Unity 项目目录不存在或结构不完整。");
        if (!File.Exists(project.EditorPath))
            throw new FileNotFoundException($"没有找到项目所需的 Unity Editor {project.EditorVersion}。请先在 Unity Hub 中安装该版本。");

        var startInfo = new ProcessStartInfo
        {
            FileName = project.EditorPath,
            Arguments = $"-projectPath \"{project.ProjectPath}\"",
            WorkingDirectory = project.ProjectPath,
            UseShellExecute = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows 没有启动 Unity Editor。");
        return process.Id;
    }

    public static bool IsUnityProjectRoot(string path) =>
        Directory.Exists(Path.Combine(path, "Assets")) &&
        Directory.Exists(Path.Combine(path, "ProjectSettings")) &&
        File.Exists(Path.Combine(path, "ProjectSettings", "ProjectVersion.txt"));

    public static string ReadEditorVersion(string projectPath)
    {
        var versionFile = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(versionFile)) return "未知版本";
        try
        {
            const string prefix = "m_EditorVersion:";
            var line = File.ReadLines(versionFile).FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(line) ? "未知版本" : line[prefix.Length..].Trim();
        }
        catch (IOException)
        {
            return "未知版本";
        }
    }

    private static string FindMatchingEditor(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version == "未知版本") return string.Empty;
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var directCandidates = new[]
        {
            Path.Combine(programFiles, "Unity", "Hub", "Editor", version, "Editor", "Unity.exe"),
            Path.Combine(programFiles, $"Unity {version}", "Editor", "Unity.exe"),
            Path.Combine(programFiles, "Unity", version, "Editor", "Unity.exe")
        };
        var exact = directCandidates.FirstOrDefault(File.Exists);
        if (exact is not null) return exact;

        var hubEditors = Path.Combine(programFiles, "Unity", "Hub", "Editor");
        if (!Directory.Exists(hubEditors)) return string.Empty;
        try
        {
            return Directory.EnumerateDirectories(hubEditors)
                .Where(x => Path.GetFileName(x).Equals(version, StringComparison.OrdinalIgnoreCase))
                .Select(x => Path.Combine(x, "Editor", "Unity.exe"))
                .FirstOrDefault(File.Exists) ?? string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }
}
