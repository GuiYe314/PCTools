using System.IO;
using System.Text.Json;
using JuDianWorkbench.Models;

namespace JuDianWorkbench.Services;

public sealed class JsonDataStore
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    private readonly string _dataFile;
    public string DataFilePath => _dataFile;

    public JsonDataStore(string? dataFile = null)
    {
        if (!string.IsNullOrWhiteSpace(dataFile))
        {
            _dataFile = dataFile;
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFile)!);
            return;
        }
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JuDianWorkbench");
        Directory.CreateDirectory(directory);
        _dataFile = Path.Combine(directory, "data.json");
    }

    public AppData Load()
    {
        if (!File.Exists(_dataFile)) return new AppData();
        try
        {
            return JsonSerializer.Deserialize<AppData>(File.ReadAllText(_dataFile), _options) ?? new AppData();
        }
        catch (Exception ex)
        {
            AppLogger.Error("读取数据文件失败", ex);
            return new AppData();
        }
    }

    public void Save(AppData data)
    {
        var tempFile = _dataFile + ".tmp";
        File.WriteAllText(tempFile, JsonSerializer.Serialize(data, _options));
        File.Move(tempFile, _dataFile, true);
    }
}
