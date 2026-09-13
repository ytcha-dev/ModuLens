using ModuLens.App.ViewModels;

namespace ModuLens.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void LoadDocument_SelectsFirstModuleAndShowsContentOnly()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * Config",
            " * ===",
            " */",
            "const enabled = true;",
            "/**",
            " * ===",
            " * Transform",
            " * ===",
            " */",
            "function applyTransform() {};");
        var viewModel = new MainWindowViewModel();

        viewModel.LoadDocument("fixture.js", source);

        Assert.Equal(2, viewModel.Sections.Count);
        Assert.Equal("Config", viewModel.SelectedSection?.Name);
        Assert.Equal("const enabled = true;\n", viewModel.SelectedSource);
        Assert.DoesNotContain("/**", viewModel.SelectedSource);
        Assert.Equal("2 modules detected.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectingModule_UpdatesSourceAndRangeSummary()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * Config",
            " * ===",
            " */",
            "const enabled = true;",
            "/**",
            " * ===",
            " * Transform",
            " * ===",
            " */",
            "function applyTransform() {};");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", source);

        viewModel.SelectedSection = Assert.Single(
            viewModel.Sections,
            section => section.Name == "Transform");

        Assert.Equal("function applyTransform() {};", viewModel.SelectedSource);
        Assert.Equal("content 12–12  •  full 7–12", viewModel.SelectedRangeSummary);
    }

    [Fact]
    public void LoadDocument_WithoutSupportedHeadingsShowsEmptyState()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.LoadDocument("plain.js", "const value = 1;");

        Assert.Empty(viewModel.Sections);
        Assert.Null(viewModel.SelectedSection);
        Assert.Equal(string.Empty, viewModel.SelectedSource);
        Assert.Equal("No module selected", viewModel.SelectedRangeSummary);
        Assert.Equal("No supported section headings were found.", viewModel.StatusMessage);
    }

    [Fact]
    public void SampleUserscript_CanSelectTransformAndDisplayItsContent()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.js");
        var source = File.ReadAllText(samplePath);
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument(samplePath, source);

        viewModel.SelectedSection = Assert.Single(
            viewModel.Sections,
            section => section.Name == "Transform");

        Assert.Equal(21, viewModel.Sections.Count);
        Assert.Contains("function applyTransform", viewModel.SelectedSource);
        Assert.DoesNotContain("* Transform", viewModel.SelectedSource);
        Assert.Equal("content 1605–1668  •  full 1600–1668", viewModel.SelectedRangeSummary);
        Assert.Equal(source, File.ReadAllText(samplePath));
    }

    [Fact]
    public void EditingSelectedModule_PreparesCompleteTextAndRecalculatesRanges()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * Config",
            " * ===",
            " */",
            "const enabled = true;",
            "/**",
            " * ===",
            " * Transform",
            " * ===",
            " */",
            "function applyTransform() {};");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", source);

        viewModel.SelectedSource = Lines(
            "const enabled = false;",
            "const retries = 3;") + "\n";

        Assert.True(viewModel.HasUnsavedChanges);
        Assert.True(viewModel.CanSave);

        var completeText = viewModel.PrepareSave();

        Assert.Contains("const enabled = false;\nconst retries = 3;\n", completeText);
        Assert.EndsWith("function applyTransform() {};", completeText);
        Assert.Equal("Config", viewModel.SelectedSection?.Name);
        Assert.Equal("content 6–7  •  full 1–7", viewModel.SelectedRangeSummary);
        Assert.Equal(8, viewModel.Sections[1].FullRange.StartLine);

        viewModel.MarkSaved();

        Assert.False(viewModel.HasUnsavedChanges);
        Assert.False(viewModel.CanSave);
        Assert.Equal("Saved. 2 modules detected.", viewModel.StatusMessage);
    }

    [Fact]
    public void SelectingAnotherModule_CommitsPendingEditToTheDocumentSnapshot()
    {
        var source = Lines(
            "/**",
            " * ===",
            " * First",
            " * ===",
            " */",
            "first();",
            "/**",
            " * ===",
            " * Second",
            " * ===",
            " */",
            "second();");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", source);
        var secondBeforeEdit = viewModel.Sections[1];
        viewModel.SelectedSource = "first();\nfirstAgain();\n";

        viewModel.SelectedSection = secondBeforeEdit;

        Assert.Equal("Second", viewModel.SelectedSection?.Name);
        Assert.Equal("second();", viewModel.SelectedSource);
        Assert.Equal(8, viewModel.SelectedSection?.FullRange.StartLine);
        Assert.Contains("firstAgain();", viewModel.PrepareSave());
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void PrepareSave_WithoutLoadedDocumentFailsExplicitly()
    {
        var viewModel = new MainWindowViewModel();

        var exception = Assert.Throws<InvalidOperationException>(viewModel.PrepareSave);

        Assert.Equal("No source document is loaded.", exception.Message);
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
