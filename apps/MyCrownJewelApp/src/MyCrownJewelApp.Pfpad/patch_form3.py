# Read Form1.cs
with open('C:\\\\Users\\\\casse\\\\github\\\\azure-ops-solo\\\\apps\\\\MyCrownJewelApp\\\\src\\\\MyCrownJewelApp.TextEditor\\\\Form1.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Remove legacy color fields
old_colors = '''        // Colors
        private Color darkBackColor = Color.FromArgb(30, 30, 30);
        private Color darkForeColor = Color.FromArgb(220, 220, 220);
        private Color darkMenuBackColor = Color.FromArgb(45, 45, 45);
        private Color darkMenuForeColor = Color.FromArgb(220, 220, 220);
        private Color darkEditorBackColor = Color.FromArgb(30, 30, 30);
        private Color darkEditorForeColor = Color.FromArgb(220, 220, 220);

        private Color lightBackColor = Color.White;
        private Color lightForeColor = Color.Black;
        private Color lightMenuBackColor = SystemColors.MenuBar;
        private Color lightMenuForeColor = SystemColors.MenuText;
        private Color lightEditorBackColor = Color.White;
        private Color lightEditorForeColor = Color.Black;

        private Color keywordColor = Color.Blue;
        private Color stringColor = Color.Maroon;
        private Color commentColor = Color.Green;
        private Color numberColor = Color.DarkRed;
        private Color preprocessorColor = Color.Gray;'''

new_colors = '''        // Colors - for syntax highlighting
        private Color keywordColor = Color.Blue;
        private Color stringColor = Color.Maroon;
        private Color commentColor = Color.Green;
        private Color numberColor = Color.DarkRed;
        private Color preprocessorColor = Color.Gray;'''

content = content.replace(old_colors, new_colors)

with open('C:\\\\Users\\\\casse\\\\github\\\\azure-ops-solo\\\\apps\\\\MyCrownJewelApp\\\\src\\\\MyCrownJewelApp.TextEditor\\\\Form1.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Removed legacy color fields')
