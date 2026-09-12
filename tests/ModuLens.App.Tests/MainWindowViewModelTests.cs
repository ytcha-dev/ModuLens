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

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
