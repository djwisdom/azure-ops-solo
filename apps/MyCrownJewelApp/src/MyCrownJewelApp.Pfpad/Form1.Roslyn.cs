using MyCrownJewelApp.Pfpad.Roslyn;

namespace MyCrownJewelApp.Pfpad;

public partial class Form1
{
    private readonly RoslynWorkspaceService _roslynWorkspace = new();
    private readonly TreeSitter.TreeSitterService _treeSitter = new();

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        WireRoslynTextSync();
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
            if (symbol is null) return;

            var def = await Task.Run(() => _roslynWorkspace.FindSourceDefinitionAsync(symbol)) ?? symbol;
            var loc = def.Locations.FirstOrDefault(l => l.IsInSource);
            if (loc?.SourceTree?.FilePath is { Length: > 0 } filePath && File.Exists(filePath))
            {
                int line = loc.GetLineSpan().StartLinePosition.Line + 1;
                BeginInvoke(() => { OpenFileInNewTab(filePath); GoToLine(line); });
                return;
            }
        }
        catch { }
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

    private void InitializeRoslynRename(RenameDialog dlg)
    {
    }
}
