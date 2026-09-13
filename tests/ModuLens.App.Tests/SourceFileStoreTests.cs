using System.Text;
using ModuLens.App.Storage;

namespace ModuLens.App.Tests;

public sealed class SourceFileStoreTests
{
    [Fact]
    public async Task SaveAsync_PreservesUtf8BomAndWritesCompleteText()
    {
        var fixture = await TemporarySourceFile.CreateAsync(
            "fixture.js",
            [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("const before = true;\n")]);

        try
        {
            var store = new SourceFileStore();
            var snapshot = await store.LoadAsync(fixture.FilePath);

            var saved = await store.SaveAsync(snapshot, "const after = true;\n");

            var bytes = await File.ReadAllBytesAsync(fixture.FilePath);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
            Assert.Equal("const after = true;\n", Encoding.UTF8.GetString(bytes[3..]));
            Assert.Equal("const after = true;\n", saved.Text);
            Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task SaveAsync_PreservesUtf16LittleEndianEncoding()
    {
        var encoding = new UnicodeEncoding(false, true, true);
        var fixture = await TemporarySourceFile.CreateAsync(
            "fixture.js",
            [.. encoding.GetPreamble(), .. encoding.GetBytes("const 名稱 = 1;\r\n")]);

        try
        {
            var store = new SourceFileStore();
            var snapshot = await store.LoadAsync(fixture.FilePath);

            await store.SaveAsync(snapshot, "const 名稱 = 2;\r\n");

            var bytes = await File.ReadAllBytesAsync(fixture.FilePath);
            Assert.Equal(encoding.GetPreamble(), bytes[..2]);
            Assert.Equal("const 名稱 = 2;\r\n", encoding.GetString(bytes[2..]));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsAnExternalChangeWithoutOverwritingIt()
    {
        var fixture = await TemporarySourceFile.CreateAsync(
            "fixture.js",
            Encoding.UTF8.GetBytes("const original = true;"));

        try
        {
            var store = new SourceFileStore();
            var snapshot = await store.LoadAsync(fixture.FilePath);
            await File.WriteAllTextAsync(fixture.FilePath, "const external = true;");

            var exception = await Assert.ThrowsAsync<SourceFileChangedException>(() =>
                store.SaveAsync(snapshot, "const editor = true;"));

            Assert.Equal(Path.GetFullPath(fixture.FilePath), exception.FilePath);
            Assert.Equal("const external = true;", await File.ReadAllTextAsync(fixture.FilePath));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private sealed class TemporarySourceFile : IDisposable
    {
        private TemporarySourceFile(string directoryPath, string filePath)
        {
            DirectoryPath = directoryPath;
            FilePath = filePath;
        }

        public string DirectoryPath { get; }

        public string FilePath { get; }

        public static async Task<TemporarySourceFile> CreateAsync(string fileName, byte[] bytes)
        {
            var directoryPath = Path.Combine(
                Path.GetTempPath(),
                $"ModuLens.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var filePath = Path.Combine(directoryPath, fileName);
            await File.WriteAllBytesAsync(filePath, bytes);
            return new TemporarySourceFile(directoryPath, filePath);
        }

        public void Dispose() => Directory.Delete(DirectoryPath, true);
    }
}
