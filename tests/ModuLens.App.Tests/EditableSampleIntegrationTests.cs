using ModuLens.App.Storage;
using ModuLens.App.ViewModels;
using ModuLens.Core.Parsing;

namespace ModuLens.App.Tests;

public sealed class EditableSampleIntegrationTests
{
    [Fact]
    public async Task EditTransformAndSave_ReconstructsCompleteUserscript()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.js");
        var originalBytes = await File.ReadAllBytesAsync(samplePath);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ModuLens.Integration.{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var editablePath = Path.Combine(temporaryDirectory, "sample.js");
        await File.WriteAllBytesAsync(editablePath, originalBytes);

        try
        {
            var store = new SourceFileStore();
            var snapshot = await store.LoadAsync(editablePath);
            var originalText = snapshot.Text;
            var originalDocument = new SectionParser().Parse(editablePath, originalText);
            var originalTransform = Assert.Single(
                originalDocument.Sections,
                section => section.Name == "Transform");
            var viewModel = new MainWindowViewModel();
            viewModel.LoadDocument(snapshot.FilePath, snapshot.Text);
            viewModel.SelectedSection = Assert.Single(
                viewModel.Sections,
                section => section.Name == "Transform");
            viewModel.SelectedSource = viewModel.SelectedSource.Replace(
                "function applyTransform",
                "function applyTransformEdited",
                StringComparison.Ordinal);

            var updatedText = viewModel.PrepareSave();
            await store.SaveAsync(snapshot, updatedText);
            viewModel.MarkSaved();

            var reloaded = await store.LoadAsync(editablePath);
            var updatedDocument = new SectionParser().Parse(editablePath, reloaded.Text);
            var updatedTransform = Assert.Single(
                updatedDocument.Sections,
                section => section.Name == "Transform");
            Assert.Equal(21, updatedDocument.Sections.Count);
            Assert.Contains("function applyTransformEdited", updatedTransform.ContentRange.GetText(reloaded.Text));
            Assert.Equal(
                originalText[..originalTransform.ContentRange.StartOffset],
                reloaded.Text[..updatedTransform.ContentRange.StartOffset]);
            Assert.Equal(
                originalText[originalTransform.ContentRange.EndOffset..],
                reloaded.Text[(updatedTransform.ContentRange.EndOffset)..]);
            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(samplePath));
            Assert.False(viewModel.HasUnsavedChanges);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }
}
