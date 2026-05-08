using Microsoft.CodeAnalysis.Formatting;
using MyCrownJewelApp.Pfpad.Features.RoslynControl;
using MyCrownJewelApp.Pfpad.Roslyn;

namespace MyCrownJewelApp.Pfpad;

public partial class Form1
{
    private readonly RoslynWorkspaceService _roslynWorkspace = new();
    private RoslynService? _roslynService;
    private RoslynVisualizerForm? _visualizerForm;
    private readonly TreeSitter.TreeSitterService _treeSitter = new();

    private RoslynService EnsureRoslynService() =>
        _roslynService ??= new RoslynService(_roslynWorkspace);

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        WireRoslynTextSync();
        InitializeRoslynCommands();
    }

    private void WireRoslynTextSync()
    {
        if (textEditor is null) return;
        textEditor.TextChanged += (s, args) =>
        {
            if (!_roslynWorkspace.IsReady) return;
            if (isHighlighting) return;
            _roslynWorkspace.UpdateDocumentText(textEditor.Text);
        };
    }

    private void OpenRoslynDocument(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        _roslynWorkspace.OpenDocument(path);
        if (_roslynWorkspace.IsReady)
        {
            _symbolIndex.SetRoslynWorkspace(_roslynWorkspace);
            _lintEngine.SetRoslynWorkspace(_roslynWorkspace);
        }
    }

    private async void GoToDefinitionRoslyn()
    {
        if (textEditor is null || !_roslynWorkspace.IsReady) return;
        string? word = GetWordAtCursor();
        if (string.IsNullOrEmpty(word)) return;

        try
        {
            int pos = textEditor.SelectionStart;
            var symbol = await Task.Run(() => _roslynWorkspace.FindSymbolAtPositionAsync(pos));
            if (symbol is null)
            {
                BeginInvoke(() => ThemedMessageBox.Show(
                    $"No definition found for '{word}'.\n\nRoslyn could not resolve this symbol. " +
                    "This may happen if:\n" +
                    "- The symbol is from an external library not referenced by the project\n" +
                    "- The file is not part of a .NET project\n" +
                    "- The project needs to be built first",
                    "Go to Definition", MessageBoxButtons.OK, MessageBoxIcon.Information));
                return;
            }

            var def = await Task.Run(() => _roslynWorkspace.FindSourceDefinitionAsync(symbol)) ?? symbol;
            var loc = def.Locations.FirstOrDefault(l => l.IsInSource);
            if (loc?.SourceTree?.FilePath is { Length: > 0 } filePath && File.Exists(filePath))
            {
                int line = loc.GetLineSpan().StartLinePosition.Line + 1;
                BeginInvoke(() => { OpenFileInNewTab(filePath); GoToLine(line); });
                return;
            }

            BeginInvoke(() => ThemedMessageBox.Show(
                $"No definition found for '{word}'.\n\nThe symbol resolves but has no source location. " +
                "It may be defined in metadata or an external assembly.",
                "Go to Definition", MessageBoxButtons.OK, MessageBoxIcon.Information));
        }
        catch (Exception ex)
        {
            BeginInvoke(() => ThemedMessageBox.Show(
                $"Error resolving '{word}': {ex.Message}",
                "Go to Definition", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
    }

    private void ShowSignatureHelpRoslyn()
    {
        if (textEditor is null || !_roslynWorkspace.IsReady) return;
        int pos = textEditor.SelectionStart;
        if (pos < 1) return;

        var sigData = _roslynWorkspace.GetSignatureHelpAsync(pos).Result;
        if (sigData is null) return;

        _hoverTooltip.Dismiss();
        string paramDoc = sigData.CurrentParameter < sigData.ParameterDocumentation.Count
            ? sigData.ParameterDocumentation[sigData.CurrentParameter]
            : "";
        Point pt = textEditor.GetPositionFromCharIndex(pos);
        Point screenPt = textEditor.PointToScreen(pt);
        _signatureHelp.ShowAt(screenPt, sigData.Signature, sigData.CurrentParameter, paramDoc);
    }

    private void ShowHoverTooltipRoslyn(string word, Point mouseLoc)
    {
        if (textEditor is null || !_roslynWorkspace.IsReady) return;

        var symbols = _roslynWorkspace.FindSymbolsAsync(word).Result;
        if (symbols.Count == 0) return;

        var first = symbols[0];
        string title = $"{first.Kind}: {_roslynWorkspace.GetSymbolDisplayString(first)}";
        string summary = _roslynWorkspace.GetXmlDocumentation(first) ?? "";
        string context = first.ContainingNamespace?.ToDisplayString() ?? "";
        Point screenLoc = textEditor.PointToScreen(mouseLoc);
        _hoverTooltip.ShowAt(screenLoc, title, summary, context);
    }

    private void InitializeRoslynCommands()
    {
        WireMenuItem(restartAnalyzersMenuItem, (s, e) => _ = RestartAnalyzersCommandAsync());
        WireMenuItem(toggleAnalyzersMenuItem, (s, e) => ToggleAnalyzersCommand());

        if (roslynDropDown?.DropDownItems is { Count: >= 3 } items)
        {
            if (items[0] is ToolStripMenuItem restartItem)
                restartItem.Click += (s, e) => _ = RestartAnalyzersCommandAsync();
            if (items[2] is ToolStripMenuItem visItem)
                visItem.Click += (s, e) => OpenVisualizerCommand();
        }

        if (roslynToggleLabel is not null)
            roslynToggleLabel.Click += OnRoslynToggleClick;
    }

    private void OnRoslynToggleClick(object? sender, EventArgs e)
    {
        try
        {
            ToggleAnalyzersCommand();
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                BeginInvoke(() => ThemedMessageBox.Show(
                    $"Click error: {ex.GetType().Name}\n{ex.Message}\n{ex.StackTrace}",
                    "Toggle Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
    }

    private void UpdateRoslynToggleLabel()
    {
        if (roslynToggleLabel is null) return;
        roslynToggleLabel.Text = EnsureRoslynService().AreAnalyzersEnabled ? "A: ON" : "A: OFF";
        roslynToggleLabel.ForeColor = EnsureRoslynService().AreAnalyzersEnabled
            ? Color.FromArgb(80, 200, 80) : Color.FromArgb(220, 120, 120);
    }

    private static void WireMenuItem(ToolStripMenuItem? item, EventHandler handler)
    {
        if (item is not null)
            item.Click += handler;
    }

    private async Task RestartAnalyzersCommandAsync()
    {
        if (!_roslynWorkspace.IsReady)
        {
            ShowNotification("Roslyn", "No document open. Open a C# file first.");
            return;
        }

        await EnsureRoslynService().RestartAnalyzersAsync();
    }

    private async void ToggleAnalyzersCommand()
    {
        try
        {
            bool newState = !EnsureRoslynService().AreAnalyzersEnabled;
            await EnsureRoslynService().SetAnalyzersEnabledAsync(newState);

            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                try
                {
                    toggleAnalyzersMenuItem.Checked = newState;
                    _analyzersEnabled = newState;
                    UpdateRoslynToggleLabel();
                    SaveSettings();
                }
                catch (Exception inner)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToggleAnalyzers] UI marshal error: {inner}");
                    ThemedMessageBox.Show(
                        $"UI update error: {inner.GetType().Name}\n{inner.Message}\n{inner.StackTrace}",
                        "Toggle Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToggleAnalyzers] Async error: {ex}");
            try
            {
                BeginInvoke(() => ThemedMessageBox.Show(
                    $"Async error: {ex.GetType().Name}\n{ex.Message}\n{ex.StackTrace}",
                    "Toggle Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
            }
            catch { /* form likely disposed, swallow */ }
        }
    }

    private void OpenVisualizerCommand()
    {
        if (_visualizerForm is null || _visualizerForm.IsDisposed)
        {
            _visualizerForm = new RoslynVisualizerForm();
            _visualizerForm.OnRefreshRequested += async () =>
            {
                var snapshot = await EnsureRoslynService().GetVisualizerSnapshotAsync();
                _visualizerForm?.BeginInvoke(() => _visualizerForm?.UpdateSnapshot(snapshot));
            };
            _visualizerForm.FormClosed += (s, e) => _visualizerForm = null;
            _visualizerForm.Show(this);
        }
        else
        {
            _visualizerForm.BringToFront();
            _visualizerForm.Focus();
            _ = RefreshVisualizerAsync();
        }
    }

    private async Task RefreshVisualizerAsync()
    {
        var snapshot = await EnsureRoslynService().GetVisualizerSnapshotAsync();
        if (_visualizerForm is not null && !_visualizerForm.IsDisposed)
            _visualizerForm.BeginInvoke(() => _visualizerForm.UpdateSnapshot(snapshot));
    }

    private void InitializeRoslynRename(RenameDialog dlg)
    {
    }

    private async void FormatDocumentAsync()
    {
        if (textEditor is null || !_roslynWorkspace.IsReady) return;
        try
        {
            var doc = await _roslynWorkspace.GetCurrentDocumentAsync();
            if (doc is null) return;
            var formatted = await Formatter.FormatAsync(doc);
            if (formatted is null) return;
            string newText = (await formatted.GetTextAsync()).ToString();
            if (string.IsNullOrEmpty(newText) || newText == textEditor.Text) return;
            textEditor.Text = newText;
            _roslynWorkspace.UpdateDocumentText(newText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FormatDocument] {ex.Message}");
        }
    }
}
