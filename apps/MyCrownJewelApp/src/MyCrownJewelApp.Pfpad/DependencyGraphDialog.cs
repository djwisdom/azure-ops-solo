using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class DependencyGraphDialog : Form
{
    private readonly TreeView _tree;
    private readonly Label _statusLabel;

    public DependencyGraphDialog(List<ProjectDependencyAnalyzer.ProjectInfo> projects)
    {
        Text = "Project Dependency Graph";
        Size = new Size(600, 450);
        MinimumSize = new Size(400, 250);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;

        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        _statusLabel = new Label
        {
            Text = $"{projects.Count} project(s)",
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Top,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = theme.TerminalHeaderBackground,
            ForeColor = theme.Muted
        };

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = theme.EditorBackground,
            ForeColor = theme.Text,
            LineColor = theme.Border,
            FullRowSelect = true,
            HideSelection = false,
            ShowLines = true,
            Font = new Font("Segoe UI", 9)
        };

        _tree.BeginUpdate();
        foreach (var proj in projects)
        {
            var projNode = new TreeNode($"{proj.Name}  [{proj.Files.Count} files]")
            {
                Tag = proj.Path,
                ForeColor = theme.Accent,
                NodeFont = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            // Project references
            if (proj.ProjectRefs.Count > 0)
            {
                var refsNode = new TreeNode($"Project References ({proj.ProjectRefs.Count})")
                {
                    ForeColor = theme.Muted,
                    NodeFont = new Font("Segoe UI", 8.5f, FontStyle.Italic)
                };
                foreach (var dep in proj.ProjectRefs)
                    refsNode.Nodes.Add(new TreeNode(dep.ProjectName) { Tag = dep.ProjectPath, ForeColor = theme.Text });
                projNode.Nodes.Add(refsNode);
            }

            // Package references
            if (proj.PackageRefs.Count > 0)
            {
                var pkgNode = new TreeNode($"Package References ({proj.PackageRefs.Count})")
                {
                    ForeColor = theme.Muted,
                    NodeFont = new Font("Segoe UI", 8.5f, FontStyle.Italic)
                };
                foreach (var pkg in proj.PackageRefs)
                    pkgNode.Nodes.Add(new TreeNode($"{pkg.Name} v{pkg.Version}") { ForeColor = theme.Text });
                projNode.Nodes.Add(pkgNode);
            }

            // Top-level files (show top 20)
            if (proj.Files.Count > 0)
            {
                int showCount = Math.Min(proj.Files.Count, 20);
                var filesNode = new TreeNode($"Files ({proj.Files.Count})")
                {
                    ForeColor = theme.Muted,
                    NodeFont = new Font("Segoe UI", 8.5f, FontStyle.Italic)
                };
                for (int i = 0; i < showCount; i++)
                    filesNode.Nodes.Add(new TreeNode(System.IO.Path.GetFileName(proj.Files[i])) { ForeColor = theme.Text });
                if (proj.Files.Count > showCount)
                    filesNode.Nodes.Add(new TreeNode($"... and {proj.Files.Count - showCount} more") { ForeColor = theme.Muted });
                projNode.Nodes.Add(filesNode);
            }

            _tree.Nodes.Add(projNode);
        }
        _tree.EndUpdate();

        if (_tree.Nodes.Count > 0) _tree.Nodes[0].Expand();

        var closeBtn = new Button
        {
            Text = "Close",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9),
            Dock = DockStyle.Bottom,
            Size = new Size(80, 28),
            BackColor = theme.Background,
            ForeColor = theme.Text,
            FlatAppearance = { BorderColor = theme.Muted },
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (s, e) => Close();

        Controls.Add(_tree);
        Controls.Add(_statusLabel);
        Controls.Add(closeBtn);
    }
}
