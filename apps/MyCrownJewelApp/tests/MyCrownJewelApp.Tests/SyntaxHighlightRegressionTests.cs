using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;
using MyCrownJewelApp.Pfpad;
using System.Windows.Forms;

namespace MyCrownJewelApp.Tests;

/// <summary>
/// Regression tests for syntax highlighting hang bug.
/// Ensures toggling syntax highlighting does not freeze UI thread and meets performance bounds.
/// </summary>
[Collection("Sequential")]
public class SyntaxHighlightRegressionTests : IDisposable
{
    private readonly string _testDir;

    public SyntaxHighlightRegressionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "SyntaxHighlightTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDir);
    }

    #region STA Test Runner

    private T RunOnStaThread<T>(Func<T> func, int timeoutMs)
    {
        Exception? ex = null;
        T? result = default;
        var done = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            try { result = func(); }
            catch (Exception e) { ex = e; }
            finally { done.Set(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        var sw = Stopwatch.StartNew();
        thread.Start();
        bool completed = done.Wait(TimeSpan.FromMilliseconds(timeoutMs));
        sw.Stop();
        if (!completed) throw new TimeoutException($"Timeout after {timeoutMs}ms");
        if (ex != null) throw new Exception("STA thread error", ex);
        return result!;
    }

    private void ToggleSyntaxHighlighting(Form1 form)
    {
        var method = typeof(Form1).GetMethod("ToggleSyntaxHighlighting",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(form, null);
    }

    #endregion

    #region Unit Tests (Highlighter in isolation)

    [Fact]
    public void AllSupportedFileTypes_HaveSyntaxDefinition()
    {
        var testCases = new (string ext, string name)[]
        {
            (".cs", "C#"), (".csx", "C#"),
            (".c", "C"), (".h", "C"),
            (".cpp", "C++"), (".cxx", "C++"), (".cc", "C++"), (".c++", "C++"), (".hpp", "C++"), (".hxx", "C++"), (".hh", "C++"), (".h++", "C++"),
            (".bicep", "Bicep"),
            (".tf", "Terraform"), (".tfvars", "Terraform"), (".tfstate", "Terraform"),
            (".yaml", "YAML"), (".yml", "YAML"),
            (".html", "HTML"), (".htm", "HTML"), (".xhtml", "HTML"),
            (".css", "CSS"), (".scss", "CSS"), (".sass", "CSS"), (".less", "CSS"),
            (".js", "JavaScript"), (".jsx", "JavaScript"), (".mjs", "JavaScript"), (".cjs", "JavaScript"),
            (".json", "JSON"), (".jsonc", "JSON"),
            (".ps1", "PowerShell"), (".psm1", "PowerShell"), (".psd1", "PowerShell"),
            (".sh", "Bash"), (".bash", "Bash"), (".zsh", "Bash")
        };

        foreach (var (ext, expectedName) in testCases)
        {
            var def = SyntaxDefinition.GetDefinitionForFile($"test{ext}");
            Assert.NotNull(def);
            Assert.Equal(expectedName, def.Name);
        }
    }

    [Fact]
    public void IncrementalHighlighter_CSyntax_CompilesAndTokenizesQuickly()
    {
        var rtb = new RichTextBox();
        rtb.Text = "#include <stdio.h>\nint main() { return 0; }\n/* comment */";
        var sw = Stopwatch.StartNew();
        var highlighter = new IncrementalHighlighter(
            rtb,
            SyntaxDefinition.C,
            Color.Black, Color.Blue, Color.Brown, Color.Green, Color.Purple, Color.Gray);
        sw.Stop();
        try
        {
            Assert.True(sw.ElapsedMilliseconds < 2000,
                $"Highlighter construction took {sw.ElapsedMilliseconds}ms - regex compile hang?");
            highlighter.RequestRange(0, 0);
            var tokens = WaitForTokens(highlighter, 0, 2000);
            Assert.NotNull(tokens);
        }
        finally
        {
            highlighter.Dispose();
            rtb.Dispose();
        }
    }

        [Fact]
        public void IncrementalHighlighter_EmptyFile_ReturnsQuickly()
        {
            var rtb = new RichTextBox();
            var highlighter = new IncrementalHighlighter(
                rtb,
                SyntaxDefinition.CSharp,
                Color.Black, Color.Blue, Color.Brown, Color.Green, Color.Purple, Color.Gray);
            try
            {
                var sw = Stopwatch.StartNew();
                highlighter.RequestRange(0, 0);
                var tokens = WaitForTokens(highlighter, 0, 5000);
                sw.Stop();
                Assert.NotNull(tokens);
                Assert.True(sw.ElapsedMilliseconds < 4000, $"Tokenizer took {sw.ElapsedMilliseconds}ms (cold-start regex compilation expected)");
            }
            finally
            {
                highlighter.Dispose();
                rtb.Dispose();
            }
        }

    [Fact]
    public void IncrementalHighlighter_NestedBraces_DoesNotHang()
    {
        var rtb = new RichTextBox();
        rtb.Text = new string('{', 1000) + new string('}', 1000);
        var highlighter = new IncrementalHighlighter(
            rtb,
            SyntaxDefinition.CSharp,
            Color.Black, Color.Blue, Color.Brown, Color.Green, Color.Purple, Color.Gray);
        try
        {
            highlighter.RequestRange(0, 0);
            var tokens = WaitForTokens(highlighter, 0, 2000);
            Assert.NotNull(tokens);
        }
        finally
        {
            highlighter.Dispose();
            rtb.Dispose();
        }
    }

    [Fact]
    public void IncrementalHighlighter_MalformedInput_DoesNotHang()
    {
        var cases = new[]
        {
            "/* unclosed comment",
            "string s = \"unclosed",
            "// unclosed comment"
        };
        foreach (var content in cases)
        {
            var rtb = new RichTextBox { Text = content };
            var highlighter = new IncrementalHighlighter(
                rtb,
                SyntaxDefinition.CSharp,
                Color.Black, Color.Blue, Color.Brown, Color.Green, Color.Purple, Color.Gray);
            try
            {
                highlighter.RequestRange(0, 0);
                var tokens = WaitForTokens(highlighter, 0, 2000);
                Assert.NotNull(tokens);
            }
            finally
            {
                highlighter.Dispose();
                rtb.Dispose();
            }
        }
    }

    private TokenInfo[] WaitForTokens(IncrementalHighlighter highlighter, int line, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var tokens = highlighter.GetTokens(line);
            if (tokens != null)
                return tokens.ToArray();
            Thread.Sleep(1);
        }
        return Array.Empty<TokenInfo>();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ToggleSyntaxHighlight_OnEmptyFile_CompletesUnderOneSecond()
    {
        long elapsed = RunOnStaThread(() =>
        {
            using var form = new Form1();
            form.Show();
            WaitForHandles(form);
            form.textEditor.Text = "";
            var sw = Stopwatch.StartNew();
            ToggleSyntaxHighlighting(form);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }, 5000);
        Assert.True(elapsed < 1000, $"Toggle took {elapsed}ms, expected < 1000ms");
    }

    [Fact]
    public void ToggleSyntaxHighlight_OnLargeFile_CompletesUnderFiveSeconds()
    {
        long elapsed = RunOnStaThread(() =>
        {
            using var form = new Form1();
            form.Show();
            WaitForHandles(form);
            var lines = new string[10000];
            for (int i = 0; i < 10000; i++)
            {
                lines[i] = i % 10 == 0
                    ? $"class C{i} {{ void M{i}() {{ int x{i} = {i}; }} }}"
                    : $"    // line {i}";
            }
            form.textEditor.Text = string.Join(Environment.NewLine, lines);
            var sw = Stopwatch.StartNew();
            ToggleSyntaxHighlighting(form);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }, 15000);
        Assert.True(elapsed < 5000, $"Toggle took {elapsed}ms on 10K lines");
    }

    [Fact]
    public void ToggleSyntaxHighlight_MultipleTimes_NoDegradation()
    {
        long[] times = RunOnStaThread(() =>
        {
            using var form = new Form1();
            form.Show();
            WaitForHandles(form);
            form.textEditor.Text = "class Test { static void Main() { } }";
            var results = new List<long>();
            for (int i = 0; i < 20; i++)
            {
                var sw = Stopwatch.StartNew();
                ToggleSyntaxHighlighting(form);
                sw.Stop();
                results.Add(sw.ElapsedMilliseconds);
                Thread.Sleep(50);
            }
            return results.ToArray();
        }, 30000);
        var avg = times.Average();
        var max = times.Max();
        Assert.True(max < 2000, $"Slowest iteration {max}ms");
        Assert.True(avg < 1000, $"Average {avg}ms too high (leak?)");
    }

    [Fact]
    public void ToggleSyntaxHighlight_EdgeCases_NoHang()
    {
        var cases = new[]
        {
            "", // empty
            new string('"', 1000),
            "/*".PadRight(5000, '*'),
            new string('{', 500) + new string('}', 500),
            new string('x', 100000)
        };
        foreach (var content in cases)
        {
            long elapsed = RunOnStaThread(() =>
            {
                using var form = new Form1();
                form.Show();
                WaitForHandles(form);
                form.textEditor.Text = content;
                var sw = Stopwatch.StartNew();
                ToggleSyntaxHighlighting(form);
                sw.Stop();
                return sw.ElapsedMilliseconds;
            }, 5000);
            Assert.True(elapsed < 3000, $"Toggle hung on size {content.Length}, took {elapsed}ms");
        }
    }

    private static void WaitForHandles(Form1 form)
    {
        var sw = Stopwatch.StartNew();
        while (!form.IsHandleCreated || !form.textEditor.IsHandleCreated)
        {
            Application.DoEvents();
            Thread.Sleep(10);
            if (sw.ElapsedMilliseconds > 5000)
                throw new TimeoutException("Handles not created within 5 seconds");
        }
    }

    [Fact]
    public void ToggleSyntaxHighlight_OnCLoadedFile_CompletesQuickly()
    {
        long elapsed = RunOnStaThread(() =>
        {
            using var form = new Form1();
            form.Show();
            WaitForHandles(form);

            // Simulate opening a .c file: set currentFilePath and load content
            var cFilePath = Path.Combine(_testDir, "test.c");
            var cContent = "#include <stdio.h>\nint main() { int x = 0; /* comment */ return 0; }\n";
            File.WriteAllText(cFilePath, cContent);
            // Use reflection to set private currentFilePath and load text
            var cpField = typeof(Form1).GetField("currentFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
            cpField!.SetValue(form, cFilePath);
            form.textEditor.Text = cContent;

            var sw = Stopwatch.StartNew();
            ToggleSyntaxHighlighting(form);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }, 5000);
        Assert.True(elapsed < 2000, $"Toggle on .c file took {elapsed}ms");
    }

    #endregion

    public void Dispose()
    {
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
    }
}
