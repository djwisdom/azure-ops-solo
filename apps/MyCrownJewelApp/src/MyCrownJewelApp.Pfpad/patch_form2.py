import re

# Read Form1.cs
with open('C:\\\\Users\\\\casse\\\\github\\\\azure-ops-solo\\\\apps\\\\MyCrownJewelApp\\\\src\\\\MyCrownJewelApp.TextEditor\\\\Form1.cs', 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Find UpdateThemeColors start and end
start_idx = None
end_idx = None
for i, line in enumerate(lines):
    if 'private void UpdateThemeColors(bool isDark)' in line and start_idx is None:
        start_idx = i
    if start_idx is not None and i > start_idx and line.strip() in ('}','') \
            and i > start_idx + 50:
        # Check if this is the method end (next non-empty line starts with private/protected/public)
        j = i + 1
        while j < len(lines) and lines[j].strip() == '':
            j += 1
        if j < len(lines) and (lines[j].strip().startswith('private') or \
           lines[j].strip().startswith('public') or \
           lines[j].strip().startswith('protected')):
            end_idx = i
            break

print(f'Found range: {start_idx+1} to {end_idx+1}')

# New method
new_method = '''        private void UpdateThemeColors(bool isDark)
        {
            var theme = isDark ? Theme.Dark : Theme.Light;
            _themeManager.CurrentTheme = theme;
            
            this.BackColor = theme.Background;
            this.ForeColor = theme.Text;
            if (menuStrip != null)
            {
                menuStrip.Renderer = new ThemeAwareMenuRenderer(theme);
                menuStrip.BackColor = theme.MenuBackground;
                menuStrip.ForeColor = theme.Text;
            }
            if (textEditor != null)
            {
                textEditor.BackColor = theme.EditorBackground;
                textEditor.ForeColor = theme.Text;
            }
            if (gutterPanel != null)
            {
                gutterPanel.BackColor = theme.EditorBackground;
                gutterPanel.ForeColor = theme.Text;
            }
            if (minimapControl != null)
            {
                minimapControl.BackColor = theme.EditorBackground;
                minimapControl.ViewportColor = isDark ? Color.FromArgb(100, Color.DodgerBlue) : Color.FromArgb(80, Color.LightBlue);
                minimapControl.ViewportBorderColor = Color.DodgerBlue;
                minimapControl.RefreshNow();
            }
            if (guidePanel != null)
            {
                guidePanel.GuideColor = isDark ? Color.FromArgb(120, 120, 120) : Color.FromArgb(120, 120, 120);
                guidePanel.Invalidate();
            }
            if (statusStrip != null)
            {
                statusStrip.BackColor = theme.PanelBackground;
                statusStrip.ForeColor = theme.Text;
                foreach (ToolStripItem item in statusStrip.Items)
                {
                    item.BackColor = theme.PanelBackground;
                    item.ForeColor = theme.Text;
                }
            }

            if (_titleBar != null)
            {
                _titleBar.UpdateTheme();
            }

            if (textEditor != null && textEditor.IsHandleCreated)
            {
                if (isDark)
                    SetWindowTheme(textEditor.Handle, DARK_MODE_SCROLLBAR, null);
                else
                    SetWindowTheme(textEditor.Handle, null, null);
            }
            
            if (syntaxHighlightingEnabled && incrementalHighlighter != null)
            {
                RequestVisibleHighlight();
            }

            darkThemeMenuItem.Checked = isDark;
            lightThemeMenuItem.Checked = !isDark;

            CreateIncrementalHighlighter();

            if (currentLineHighlightMode != CurrentLineHighlightMode.Off)
            {
                _suspendSelectionChanged = true;
                try
                {
                    ClearCurrentLineHighlight();
                    lastHighlightedLine = -1;
                    HighlightCurrentLine();
                }
                finally
                {
                    _suspendSelectionChanged = false;
                }
            }
            gutterPanel?.RefreshGutter();
            textEditor?.Invalidate();
        }
'''

# Replace
new_lines = lines[:start_idx] + [new_method] + lines[end_idx+1:]

with open('C:\\\\Users\\\\casse\\\\github\\\\azure-ops-solo\\\\apps\\\\MyCrownJewelApp\\\\src\\\\MyCrownJewelApp.TextEditor\\\\Form1.cs', 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print('Updated UpdateThemeColors')
