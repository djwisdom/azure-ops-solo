import re

# Read Form1.cs
with open('C:\\\\Users\\\\casse\\\\github\\\\azure-ops-solo\\\\apps\\\\MyCrownJewelApp\\\\src\\\\MyCrownJewelApp.TextEditor\\\\Form1.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Add theme fields after fontSize
content = content.replace(
    '        private float fontSize = 12f;\n\n        // Feature toggles',
    '''        private float fontSize = 12f;
        
        // Theme management
        private ThemeManager _themeManager = ThemeManager.Instance;
        private TitleBarControl? _titleBar;

        // Feature toggles'''
)

# 2. Update constructor to add title bar
content = content.replace(
    '''            // Load persisted settings (overrides defaults below)
            LoadSettings();
            
            // Apply loaded font after settings are loaded
            try { textEditor.Font = new Font(fontName, fontSize); } catch { }
            
            // Subscribe to handle creation BEFORE any operations that might cause handle creation''',
    '''            // Load persisted settings (overrides defaults below)
            LoadSettings();
            
            // Apply loaded font after settings are loaded
            try { textEditor.Font = new Font(fontName, fontSize); } catch { }
            
            // Set form border to None BEFORE title bar creation
            this.FormBorderStyle = FormBorderStyle.None;
            
            // Initialize theme-aware title bar
            _titleBar = new TitleBarControl(this, _themeManager.CurrentTheme);
            
            // Subscribe to theme changes for runtime updates
            _themeManager.ThemeChanged += OnThemeChanged;
            
            // Subscribe to handle creation BEFORE any operations that might cause handle creation'''
)

# 3. Update ToggleTheme
content = content.replace(
    '''        private void ToggleTheme()
        {
            isDarkTheme = !isDarkTheme;
            UpdateThemeColors(isDarkTheme);
            // Update theme menu checkmarks
            darkThemeMenuItem.Checked = isDarkTheme;
            lightThemeMenuItem.Checked = !isDarkTheme;
        }''',
    '''        private void ToggleTheme()
        {
            _themeManager.ToggleTheme();
            isDarkTheme = _themeManager.IsDarkMode;
            UpdateThemeColors(isDarkTheme);
            // Update theme menu checkmarks
            darkThemeMenuItem.Checked = isDarkTheme;
            lightThemeMenuItem.Checked = !isDarkTheme;
        }

        private void OnThemeChanged(Theme theme)
        {
            if (this.InvokeRequired)
                this.Invoke(new Action(() => OnThemeChanged(theme)));
            else
            {
                isDarkTheme = theme.Equals(Theme.Dark);
                UpdateThemeColors(isDarkTheme);
                darkThemeMenuItem.Checked = isDarkTheme;
                lightThemeMenuItem.Checked = !isDarkTheme;
            }
        }'''
)

# Write back
with open('C:\\\\Users\\\\casse\\\\github\\\\azure-ops-solo\\\\apps\\\\MyCrownJewelApp\\\\src\\\\MyCrownJewelApp.TextEditor\\\\Form1.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Step 1-3 done')
