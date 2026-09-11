using System.Text;
using JuDianFileShare.Server.Models;
using JuDianFileShare.Server.Services;

var testRoot = Path.Combine(Path.GetTempPath(), "JuDianFileShareTests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);

try
{
    var store = new FileStore(testRoot, new FileShareOptions { StoragePath = "files", MaxFileSizeBytes = 1024 });
    var content = Encoding.UTF8.GetBytes("聚点文件共享测试");
    await using var source = new MemoryStream(content);
    var saved = await store.SaveAsync(source, @"..\测试文件.txt", content.Length);

    Assert(saved.OriginalName == "测试文件.txt", "上传文件名没有移除路径部分");
    Assert(saved.Size == content.Length, "文件大小记录错误");
    Assert(saved.Sha256.Length == 64, "SHA-256 没有正确生成");

    var listed = await store.ListAsync("测试");
    Assert(listed.Count == 1 && listed[0].Id == saved.Id, "文件列表或搜索错误");

    var found = await store.FindAsync(saved.Id);
    Assert(found is not null && File.Exists(found.Value.Path), "上传文件没有保存到磁盘");
    Assert((await File.ReadAllBytesAsync(found!.Value.Path)).SequenceEqual(content), "磁盘文件内容不一致");

    var restarted = new FileStore(testRoot, new FileShareOptions { StoragePath = "files", MaxFileSizeBytes = 1024 });
    Assert((await restarted.ListAsync(null)).Single().Id == saved.Id, "重启后文件索引丢失");

    await using var oversized = new MemoryStream(new byte[1025]);
    var rejected = false;
    try { await restarted.SaveAsync(oversized, "too-large.bin", oversized.Length); }
    catch (InvalidOperationException) { rejected = true; }
    Assert(rejected, "超出大小限制的文件未被拒绝");

    Assert(await restarted.DeleteAsync(saved.Id), "删除文件失败");
    Assert(await restarted.FindAsync(saved.Id) is null, "删除后仍可查到文件");
    Assert((await restarted.ListAsync(null)).Count == 0, "删除后索引没有更新");

    Console.WriteLine("PASS: 文件名隔离、上传、哈希、搜索、持久化、大小限制和删除全部通过。");
}
finally
{
    if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
