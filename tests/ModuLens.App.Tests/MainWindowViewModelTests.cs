using ModuLens.App.ViewModels;
using ModuLens.Core.Git;

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

    [Fact]
    public void ApplyGitBaseline_ProjectsEveryChangeKindIntoModuleExplorer()
    {
        var head = Section("Keep", "keep();\n") +
            Section("Change", "before();\n") +
            Section("Remove", "remove();\n");
        var working = Section("Keep", "keep();\n") +
            Section("Change", "after();\n") +
            Section("Add", "add();\n");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", working);

        viewModel.ApplyGitBaseline(GitFileBaseline.Available("abcdef123456", head));

        Assert.Collection(
            viewModel.Modules,
            module => Assert.Equal(SectionChangeKind.Unchanged, module.GitStatus),
            module => Assert.Equal(SectionChangeKind.Modified, module.GitStatus),
            module => Assert.Equal(SectionChangeKind.Added, module.GitStatus),
            module => Assert.Equal(SectionChangeKind.Removed, module.GitStatus));
        Assert.Equal("Working tree vs abcdef1: 3 changed modules.", viewModel.GitStatusMessage);
    }

    [Fact]
    public void SelectingRemovedModule_DisablesCurrentSourceEditor()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", Section("Current", "current();\n"));
        viewModel.ApplyGitBaseline(GitFileBaseline.Available(
            "abc123",
            Section("Current", "current();\n") + Section("Removed", "removed();\n")));

        viewModel.SelectedModule = Assert.Single(
            viewModel.Modules,
            module => module.GitStatus == SectionChangeKind.Removed);

        Assert.Null(viewModel.SelectedSection);
        Assert.False(viewModel.HasSelectedSection);
        Assert.Equal(string.Empty, viewModel.SelectedSource);
        Assert.Equal("Removed from working tree", viewModel.SelectedRangeSummary);
    }

    [Fact]
    public void ApplyGitBaseline_WhenFileIsAbsentAtHeadMarksCurrentModulesAdded()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument(
            "fixture.js",
            Section("First", "first();\n") + Section("Second", "second();\n"));

        viewModel.ApplyGitBaseline(GitFileBaseline.NotPresentAtHead());

        Assert.All(
            viewModel.Modules,
            module => Assert.Equal(SectionChangeKind.Added, module.GitStatus));
        Assert.Equal(
            "Working tree: not present at HEAD; 2 added modules.",
            viewModel.GitStatusMessage);
    }

    [Fact]
    public void PrepareSave_RecomputesGitStatusForEditedSource()
    {
        var source = Section("Tracked", "before();\n");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", source);
        viewModel.ApplyGitBaseline(GitFileBaseline.Available("abc123", source));
        viewModel.SelectedSource = "after();\n";

        viewModel.PrepareSave();

        Assert.Equal(SectionChangeKind.Modified, Assert.Single(viewModel.Modules).GitStatus);
        Assert.Equal("Current source vs abc123: 1 changed module.", viewModel.GitStatusMessage);
    }

    [Fact]
    public void ApplyGitBaseline_ReportsChangesOutsideDetectedModules()
    {
        var section = Section("Stable", "stable();\n");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", "// working preamble\n" + section);

        viewModel.ApplyGitBaseline(GitFileBaseline.Available(
            "abc123",
            "// head preamble\n" + section));

        Assert.Equal(SectionChangeKind.Unchanged, Assert.Single(viewModel.Modules).GitStatus);
        Assert.Equal(
            "Working tree vs abc123: modules unchanged; source outside detected modules changed.",
            viewModel.GitStatusMessage);
    }

    [Fact]
    public void ApplyGitBaseline_PreservesPendingEditorBuffer()
    {
        var source = Section("Tracked", "before();\n");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", source);
        viewModel.SelectedSource = "after();\n";

        viewModel.ApplyGitBaseline(GitFileBaseline.Available("abc123", source));

        Assert.True(viewModel.HasUnsavedChanges);
        Assert.Equal(SectionChangeKind.Modified, Assert.Single(viewModel.Modules).GitStatus);
        Assert.Contains("after();", viewModel.PrepareSave());
    }

    [Fact]
    public void ApplyGitBaseline_WithWorkingTreeFilteredHeadAvoidsEolFalsePositive()
    {
        var lfSource = Section("Tracked", "tracked();\n");
        var crlfSource = lfSource.Replace("\n", "\r\n", StringComparison.Ordinal);
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", crlfSource);

        viewModel.ApplyGitBaseline(GitFileBaseline.Available("abc123", crlfSource));

        Assert.Equal(SectionChangeKind.Unchanged, Assert.Single(viewModel.Modules).GitStatus);
        Assert.Equal(
            "Working tree vs abc123: all 1 module unchanged.",
            viewModel.GitStatusMessage);
    }

    [Fact]
    public void SelectingModifiedModule_OpensFullRangeSideBySideDiff()
    {
        var head = "// HEAD preamble\n" + Section("Change", "before();\nkeep();\n");
        var working = "// working preamble\n" + Section("Change", "after();\nkeep();\n");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", working);

        viewModel.ApplyGitBaseline(GitFileBaseline.Available("abcdef123456", head));

        Assert.Equal(1, viewModel.SelectedDetailTabIndex);
        Assert.True(viewModel.CanShowDiff);
        Assert.Equal("HEAD: full lines 2–8", viewModel.HeadDiffRangeSummary);
        Assert.Equal("Working Tree: full lines 2–8", viewModel.WorkingDiffRangeSummary);
        Assert.Equal("abcdef1 comparison • 1 modified", viewModel.DiffSummary);
        Assert.Equal("/**", viewModel.SelectedDiffLines[0].HeadText);
        var modified = Assert.Single(
            viewModel.SelectedDiffLines,
            line => line.KindLabel == "modified");
        Assert.Equal("7", modified.HeadLineNumber);
        Assert.Equal("before();", modified.HeadText);
        Assert.Equal("7", modified.WorkingLineNumber);
        Assert.Equal("after();", modified.WorkingText);
    }

    [Fact]
    public void SelectingAddedAndRemovedModulesShowsAbsentSide()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument(
            "fixture.js",
            Section("Keep", "keep();\n") + Section("Added", "added();\n"));
        viewModel.ApplyGitBaseline(GitFileBaseline.Available(
            "abc123",
            Section("Keep", "keep();\n") + Section("Removed", "removed();\n")));

        viewModel.SelectedModule = Assert.Single(
            viewModel.Modules,
            module => module.GitStatus == SectionChangeKind.Added);

        Assert.Equal("HEAD: absent", viewModel.HeadDiffRangeSummary);
        Assert.All(viewModel.SelectedDiffLines, line =>
        {
            Assert.Equal(string.Empty, line.HeadLineNumber);
            Assert.Equal("added", line.KindLabel);
        });

        viewModel.SelectedModule = Assert.Single(
            viewModel.Modules,
            module => module.GitStatus == SectionChangeKind.Removed);

        Assert.Equal(1, viewModel.SelectedDetailTabIndex);
        Assert.Equal("Working Tree: absent", viewModel.WorkingDiffRangeSummary);
        Assert.All(viewModel.SelectedDiffLines, line =>
        {
            Assert.Equal(string.Empty, line.WorkingLineNumber);
            Assert.Equal("removed", line.KindLabel);
        });
    }

    [Fact]
    public void OpeningDiffTab_AppliesPendingEditorBufferAndRefreshesDiff()
    {
        var source = Section("Tracked", "before();\n");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", source);
        viewModel.ApplyGitBaseline(GitFileBaseline.Available("abc123", source));
        Assert.Equal(0, viewModel.SelectedDetailTabIndex);
        viewModel.SelectedSource = "after();\n";

        viewModel.SelectedDetailTabIndex = 1;

        Assert.Equal(SectionChangeKind.Modified, viewModel.SelectedModule?.GitStatus);
        Assert.Equal("abc123 comparison • 1 modified", viewModel.DiffSummary);
        var modified = Assert.Single(
            viewModel.SelectedDiffLines,
            line => line.KindLabel == "modified");
        Assert.Equal("before();", modified.HeadText);
        Assert.Equal("after();", modified.WorkingText);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void ChangedModuleNavigation_SkipsUnchangedModulesWithoutWrapping()
    {
        var head = Section("First", "before();\n") +
            Section("Stable", "stable();\n") +
            Section("Last", "old();\n");
        var working = Section("First", "after();\n") +
            Section("Stable", "stable();\n") +
            Section("Last", "new();\n");
        var viewModel = new MainWindowViewModel();
        viewModel.LoadDocument("fixture.js", working);
        viewModel.ApplyGitBaseline(GitFileBaseline.Available("abc123", head));

        Assert.Equal("First", viewModel.SelectedModule?.Name);
        Assert.False(viewModel.CanSelectPreviousChangedModule);
        Assert.True(viewModel.CanSelectNextChangedModule);

        viewModel.SelectNextChangedModule();

        Assert.Equal("Last", viewModel.SelectedModule?.Name);
        Assert.True(viewModel.CanSelectPreviousChangedModule);
        Assert.False(viewModel.CanSelectNextChangedModule);
        viewModel.SelectNextChangedModule();
        Assert.Equal("Last", viewModel.SelectedModule?.Name);

        viewModel.SelectPreviousChangedModule();
        Assert.Equal("First", viewModel.SelectedModule?.Name);
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);

    private static string Section(string name, string content) =>
        $"/**\n * ===\n * {name}\n * ===\n */\n{content}";
}
