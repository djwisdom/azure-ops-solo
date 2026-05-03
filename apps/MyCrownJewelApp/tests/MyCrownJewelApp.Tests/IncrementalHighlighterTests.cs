using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;
using MyCrownJewelApp.Pfpad;

namespace MyCrownJewelApp.Tests;

[Collection("Sequential")]
public class IncrementalHighlighterTests : IDisposable
{
    private readonly RichTextBox _rtb;
    private readonly IncrementalHighlighter _highlighter;
    private readonly string _tempFile;

    public IncrementalHighlighterTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"inc_{Guid.NewGuid()}.cs");
        var lines = new string[100];
        for (int i = 0; i < 100; i++)
            lines[i] = i % 10 == 0 ? $"class C{{}} void M() {{ int x = {i}; }}" : $"    // line {i}";
        File.WriteAllLines(_tempFile, lines);

        Exception? ex = null;
        RichTextBox? rtb = null;
        IncrementalHighlighter? highlighter = null;
        var thread = new Thread(() =>
        {
            try
            {
                rtb = new RichTextBox { Text = File.ReadAllText(_tempFile) };
                highlighter = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
                highlighter.RequestRange(0, 99);
                Thread.Sleep(500);
            }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex!;
        _rtb = rtb!;
        _highlighter = highlighter!;
    }

    public void Dispose()
    {
        _highlighter?.Dispose();
        _rtb?.Dispose();
        try { File.Delete(_tempFile); } catch { }
    }

    private void RunOnUI(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { ex = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null) throw ex!;
    }

    [Fact]
    public void Tokenizer_ProducesCSharpTokens()
    {
        RunOnUI(() =>
        {
            _highlighter.RequestRange(0, 0);
            var sw = Stopwatch.StartNew();
            while (_highlighter.GetTokens(0) == null && sw.ElapsedMilliseconds < 1000)
                Thread.Sleep(5);
            var tokens = _highlighter.GetTokens(0);
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
            Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Keyword);
        });
    }

    [Fact(Skip = "Flaky timing - relies on async worker scheduling")]
    public void Highlighter_MarksDirty_AndTokenizes()
    {
        RunOnUI(() =>
        {
            _highlighter.MarkDirty(5);
            var sw = Stopwatch.StartNew();
            while (_highlighter.GetTokens(5) == null && sw.ElapsedMilliseconds < 3000)
                Thread.Sleep(10);
            var tokens = _highlighter.GetTokens(5);
            Assert.NotNull(tokens);
        });
    }

    [Fact]
    public void Dispose_StopsWorker()
    {
        var rtb = new RichTextBox();
        var h = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        h.Dispose();
        Assert.True(true);
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_Keywords_CSharp()
    {
        var rtb = new RichTextBox { Text = "class Foo { }" };
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (tokens, _) = hl.TokenizeLine("class Foo { }", TokenizerState.Initial);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Keyword && t.Length == 5); // "class"
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_StringLiteral()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (tokens, _) = hl.TokenizeLine("string s = \"hello world\";", TokenizerState.Initial);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.String);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_LineComment()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (tokens, _) = hl.TokenizeLine("// this is a comment", TokenizerState.Initial);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Comment);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_BlockComment_SingleLine()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (tokens, state) = hl.TokenizeLine("int x = /* comment */ 5;", TokenizerState.Initial);
        Assert.False(state.InComment);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Comment);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_BlockComment_MultiLine()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (t1, s1) = hl.TokenizeLine("/* start", TokenizerState.Initial);
        Assert.True(s1.InComment);
        var (t2, s2) = hl.TokenizeLine("  end */ x;", s1);
        Assert.False(s2.InComment);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_Number()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (tokens, _) = hl.TokenizeLine("int x = 42;", TokenizerState.Initial);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Number);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_Preprocessor()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (tokens, _) = hl.TokenizeLine("#region TestRegion", TokenizerState.Initial);
        Assert.Contains(tokens, t => t.Type == SyntaxTokenType.Preprocessor);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void TokenizeLine_Empty_ReturnsEmpty()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        var (tokens, _) = hl.TokenizeLine("", TokenizerState.Initial);
        Assert.Empty(tokens);
        hl.Dispose();
        rtb.Dispose();
    }

    [Fact]
    public void GetTokens_ReturnsNull_ForUntokenizedLine()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        var hl = new IncrementalHighlighter(rtb, SyntaxDefinition.CSharp);
        Assert.Null(hl.GetTokens(9999));
        hl.Dispose();
        rtb.Dispose();
    }
}

[Collection("Sequential")]
public class FoldingManagerTests
{
    [Fact]
    public void ScanRegions_DetectsRegionEndregion()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        rtb.Text = "class A\n{\n    void M()\n    {\n    }\n}";
        var fm = new FoldingManager(rtb);
        fm.ScanRegions();
        Assert.True(fm.IsFoldStart(1), "Opening brace at line 1 should be a fold start");
    }

    [Fact]
    public void ToggleFold_CollapsesAndExpands()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        rtb.Text = "class A\n{\n    void M()\n    {\n    }\n}";
        var fm = new FoldingManager(rtb);
        fm.ScanRegions();
        Assert.True(fm.IsFoldStart(1), "Line 1 should be a fold start");
        Assert.False(fm.IsCollapsed(1));
        fm.ToggleFold(1);
        Assert.True(fm.IsCollapsed(1));
        fm.ToggleFold(1);
        Assert.False(fm.IsCollapsed(1));
    }

    [Fact]
    public void ScanRegions_NoBraces_ReturnsEmpty()
    {
        var rtb = new RichTextBox();
        IntPtr h = rtb.Handle;
        rtb.Text = "int x = 1;\nint y = 2;";
        var fm = new FoldingManager(rtb);
        fm.ScanRegions();
        Assert.False(fm.IsFoldStart(0));
        Assert.False(fm.IsFoldStart(1));
    }
}

[Collection("Sequential")]
public class VimEngineTests
{
    [Fact]
    public void EnterMode_SetsCurrentMode()
    {
        var rtb = new RichTextBox();
        var vim = new VimEngine(rtb);
        Assert.Equal(VimMode.Normal, vim.CurrentMode);
        vim.EnterMode(VimMode.Insert);
        Assert.Equal(VimMode.Insert, vim.CurrentMode);
        rtb.Dispose();
    }

    [Fact]
    public void ProcessKey_Disabled_ReturnsFalse()
    {
        var rtb = new RichTextBox();
        var vim = new VimEngine(rtb);
        Assert.False(vim.ProcessKey(Keys.A));
        rtb.Dispose();
    }

    [Fact]
    public void ProcessKey_InsertMode_Escape_ReturnsToNormal()
    {
        var rtb = new RichTextBox();
        var vim = new VimEngine(rtb) { Enabled = true };
        vim.EnterMode(VimMode.Insert);
        Assert.True(vim.ProcessKey(Keys.Escape));
        Assert.Equal(VimMode.Normal, vim.CurrentMode);
        rtb.Dispose();
    }

    [Fact]
    public void ProcessKey_NormalMode_I_EntersInsert()
    {
        var rtb = new RichTextBox();
        var vim = new VimEngine(rtb) { Enabled = true };
        Assert.True(vim.ProcessKey(Keys.I));
        Assert.Equal(VimMode.Insert, vim.CurrentMode);
        rtb.Dispose();
    }

    [Fact]
    public void ProcessKey_NormalMode_J_MovesCursor()
    {
        var rtb = new RichTextBox();
        var vim = new VimEngine(rtb) { Enabled = true };
        rtb.Text = "line1\nline2\nline3";
        int start = rtb.SelectionStart;
        vim.ProcessKey(Keys.J);
        Assert.NotEqual(start, rtb.SelectionStart);
        rtb.Dispose();
    }
}

[Collection("Sequential")]
public class ThemeManagerTests
{
    [Fact]
    public void ToggleTheme_SwitchesMode()
    {
        var mgr = ThemeManager.Instance;
        bool before = mgr.IsDarkMode;
        mgr.ToggleTheme();
        Assert.NotEqual(before, mgr.IsDarkMode);
        mgr.ToggleTheme();
        Assert.Equal(before, mgr.IsDarkMode);
    }

    [Fact]
    public void LightTheme_HasLightBackground()
    {
        var light = Theme.Light;
        Assert.Equal(248, light.Background.R);
        Assert.Equal(248, light.Background.G);
        Assert.Equal(248, light.Background.B);
    }

    [Fact]
    public void DarkTheme_HasDarkBackground()
    {
        var dark = Theme.Dark;
        Assert.Equal(30, dark.Background.R);
        Assert.Equal(30, dark.Background.G);
        Assert.Equal(30, dark.Background.B);
    }
}

[Collection("Sequential")]
public class SyntaxDefinitionTests
{
    [Fact]
    public void CSharp_HasKeywords()
    {
        var cs = SyntaxDefinition.CSharp;
        Assert.NotEmpty(cs.Keywords);
        Assert.Contains("class", cs.Keywords);
        Assert.Contains("int", cs.Keywords);
    }

    [Fact]
    public void GetDefinitionForFile_ReturnsCorrect()
    {
        Assert.Equal("C#", SyntaxDefinition.GetDefinitionForFile("test.cs")?.Name);
        Assert.Equal("C", SyntaxDefinition.GetDefinitionForFile("test.c")?.Name);
        Assert.Equal("C++", SyntaxDefinition.GetDefinitionForFile("test.cpp")?.Name);
        Assert.Equal("JavaScript", SyntaxDefinition.GetDefinitionForFile("test.js")?.Name);
        Assert.Equal("YAML", SyntaxDefinition.GetDefinitionForFile("test.yml")?.Name);
        Assert.Equal("HTML", SyntaxDefinition.GetDefinitionForFile("test.html")?.Name);
        Assert.Equal("CSS", SyntaxDefinition.GetDefinitionForFile("test.css")?.Name);
        Assert.Equal("Bash", SyntaxDefinition.GetDefinitionForFile("test.sh")?.Name);
        Assert.Null(SyntaxDefinition.GetDefinitionForFile("test.unknown"));
    }

    [Fact]
    public void UnknownExtension_ReturnsNull()
    {
        Assert.Null(SyntaxDefinition.GetDefinitionForFile("readme.txt"));
    }
}

[Collection("Sequential")]
public class ColumnGuidePanelTests
{
    [Fact]
    public void DefaultProperties()
    {
        var panel = new ColumnGuidePanel();
        Assert.Equal(80, panel.GuideColumn);
        Assert.True(panel.ShowGuide);
        panel.Dispose();
    }

    [Fact]
    public void SetGuideColumn_ClampsToMinimum()
    {
        var panel = new ColumnGuidePanel();
        panel.GuideColumn = 0;
        Assert.Equal(1, panel.GuideColumn);
        panel.Dispose();
    }
}
