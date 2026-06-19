using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FluentAssertions;
using MyCrownJewelApp.Pfpad;
using Xunit;

namespace MyCrownJewelApp.Tests;

[Collection("Sequential")]
public class SyntaxHighlightRegressionTests
{
    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        using ManualResetEventSlim completed = new(false);

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        completed.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("the STA test should complete within the timeout.");
        if (ex is not null)
        {
            throw ex;
        }
    }

    private static IncrementalHighlighter CreateHighlighter(RichTextBox rtb, SyntaxDefinition syn)
    {
        IntPtr handle = rtb.Handle;
        return new IncrementalHighlighter(rtb, syn);
    }

    private static void WaitForPatch(IncrementalHighlighter highlighter, Action request)
    {
        using ManualResetEventSlim ready = new(false);

        highlighter.PatchReady += OnPatchReady;
        try
        {
            var sw = Stopwatch.StartNew();
            request();
            while (!ready.IsSet && sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                ready.Wait(TimeSpan.FromMilliseconds(10));
            }

            ready.IsSet.Should().BeTrue("PatchReady should fire within the timeout.");
        }
        finally
        {
            highlighter.PatchReady -= OnPatchReady;
        }

        void OnPatchReady(List<HighlightPatch> _)
        {
            ready.Set();
        }
    }

    [Fact]
    public void ToggleSyntaxHighlighting_DoesNotHang()
    {
        RunOnSta(() =>
        {
            using var rtb = new RichTextBox
            {
                Text = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"int x{i} = {i};"))
            };

            var sw = Stopwatch.StartNew();
            using var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
            WaitForPatch(hl, () => hl.RequestRange(0, 99));
            sw.Stop();

            hl.GetTokens(0).Should().NotBeNull();
            sw.ElapsedMilliseconds.Should().BeLessThan(5000);
        });
    }

    [Fact]
    public void LargeFile_ProcessesWithinTime()
    {
        RunOnSta(() =>
        {
            using var rtb = new RichTextBox();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 500; i++)
            {
                sb.AppendLine($"class C{i} {{ void M{i}() {{ if (true) {{ int x = {i}; }} }} }}");
            }

            rtb.Text = sb.ToString();
            using var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
            var sw = Stopwatch.StartNew();

            WaitForPatch(hl, () => hl.RequestRange(0, 499));

            sw.Stop();
            hl.GetTokens(0).Should().NotBeNull();
            sw.ElapsedMilliseconds.Should().BeLessThan(10000);
        });
    }

    [Fact]
    public void C_Syntax_KeywordsHighlighted()
    {
        RunOnSta(() =>
        {
            using var rtb = new RichTextBox { Text = "int main() { return 0; }" };
            using var hl = CreateHighlighter(rtb, SyntaxDefinition.C);

            WaitForPatch(hl, () => hl.RequestRange(0, 0));

            var tokens = hl.GetTokens(0);
            tokens.Should().NotBeNull();
            tokens.Should().Contain(t => t.Type == SyntaxTokenType.Keyword);
        });
    }

    [Fact]
    public void CSharp_MultipleLineTypes()
    {
        RunOnSta(() =>
        {
            using var rtb = new RichTextBox { Text = "class Foo\n{\n    // comment\n    int x = \"hello\";\n}" };
            using var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);

            WaitForPatch(hl, () => hl.RequestRange(0, 4));

            hl.GetTokens(0).Should().NotBeNull();
            hl.GetTokens(2).Should().NotBeNull();
            hl.GetTokens(3).Should().NotBeNull();
        });
    }

    [Fact]
    public void UnclosedComment_DoesNotHang()
    {
        RunOnSta(() =>
        {
            using var rtb = new RichTextBox { Text = "/* unclosed comment\nthat spans\nmultiple lines\nwithout closing" };
            using var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
            var sw = Stopwatch.StartNew();

            WaitForPatch(hl, () => hl.RequestRange(0, 3));

            sw.Stop();
            hl.GetTokens(0).Should().NotBeNull();
            sw.ElapsedMilliseconds.Should().BeLessThan(2000);
        });
    }

    [Fact]
    public void RapidToggle_DoesNotDegrade()
    {
        RunOnSta(() =>
        {
            using var rtb = new RichTextBox
            {
                Text = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"int x{i} = {i};"))
            };

            for (int t = 0; t < 5; t++)
            {
                using var hl = CreateHighlighter(rtb, SyntaxDefinition.CSharp);
                WaitForPatch(hl, () => hl.RequestRange(0, 49));
                hl.GetTokens(0).Should().NotBeNull();
            }
        });
    }
}
