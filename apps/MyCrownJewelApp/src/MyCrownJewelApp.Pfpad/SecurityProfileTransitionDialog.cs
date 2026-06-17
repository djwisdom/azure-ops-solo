using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class SecurityProfileTransitionDialog : Form
{
    private readonly SecurityProfile _from;
    private readonly SecurityProfile _to;
    private readonly Theme _theme;
    private readonly List<TransitionStep> _steps;
    private readonly Panel _stepListPanel;
    private readonly Label _summaryLabel;
    private readonly Button _actionButton;
    private readonly List<(Label icon, Label title, Label detail)> _stepRows = [];
    private bool _running;
    private bool _rollbackRequired;

    public bool TransitionCompleted { get; private set; }

    public SecurityProfileTransitionDialog(
        SecurityProfile from,
        SecurityProfile to,
        SecurityProfileTransitionService service,
        Theme theme)
    {
        _from = from;
        _to = to;
        _theme = theme;
        _steps = service.GetSteps(from, to);

        Text = "Security Profile Change";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 440);
        BackColor = theme.Background;
        ForeColor = theme.Text;
        Padding = new Padding(18);
        Font = new Font("Segoe UI", 9f);

        bool upgrading = to > from;
        string arrow = upgrading ? "↑" : "↓";

        var header = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 34,
            Text = $"Security Profile: {_from} {arrow} {_to}",
            ForeColor = upgrading ? theme.Accent : Color.FromArgb(220, 170, 60),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        var subLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 24,
            Text = upgrading
                ? "Hardening changes are being applied automatically."
                : "Reduced protections are being applied automatically.",
            ForeColor = theme.Muted,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.Transparent
        };

        _summaryLabel = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 24,
            Text = _steps.Count == 0 ? "No migration steps required." : "Preparing transition…",
            ForeColor = theme.Muted,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
            BackColor = Color.Transparent
        };

        _stepListPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = theme.PanelBackground,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12)
        };
        BuildStepRows();

        _actionButton = new Button
        {
            Text = _steps.Count == 0 ? "Close" : "Cancel",
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Size = new Size(110, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.PanelBackground,
            ForeColor = theme.Text,
            Cursor = Cursors.Hand
        };
        _actionButton.FlatAppearance.BorderColor = theme.Border;
        _actionButton.Click += ActionButton_Click;

        var buttonHost = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            BackColor = Color.Transparent
        };
        buttonHost.Controls.Add(_actionButton);
        buttonHost.Resize += (_, _) =>
        {
            _actionButton.Location = new Point(buttonHost.ClientSize.Width - _actionButton.Width, 6);
        };

        Controls.Add(_stepListPanel);
        Controls.Add(_summaryLabel);
        Controls.Add(subLabel);
        Controls.Add(header);
        Controls.Add(buttonHost);

        Shown += async (_, _) =>
        {
            if (_steps.Count == 0)
            {
                TransitionCompleted = true;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            await RunStepsAsync();
        };
    }

    private void BuildStepRows()
    {
        _stepRows.Clear();
        _stepListPanel.Controls.Clear();

        int y = 0;
        foreach (var step in _steps)
        {
            var iconLbl = new Label
            {
                Text = "⬜",
                Location = new Point(0, y + 2),
                Size = new Size(26, 20),
                ForeColor = _theme.Muted,
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.Transparent
            };
            var titleLbl = new Label
            {
                Text = step.Title,
                Location = new Point(30, y),
                Size = new Size(465, 20),
                ForeColor = _theme.Text,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            var detailLbl = new Label
            {
                Text = step.Description,
                Location = new Point(30, y + 19),
                Size = new Size(465, 32),
                ForeColor = _theme.Muted,
                Font = new Font("Segoe UI", 8f),
                BackColor = Color.Transparent
            };

            _stepListPanel.Controls.AddRange([iconLbl, titleLbl, detailLbl]);
            _stepRows.Add((iconLbl, titleLbl, detailLbl));
            y += 54;
        }
    }

    private void UpdateRow(int index)
    {
        if (index < 0 || index >= _steps.Count || index >= _stepRows.Count) return;

        var step = _steps[index];
        var (icon, title, detail) = _stepRows[index];
        (icon.Text, icon.ForeColor, title.ForeColor, detail.Text) = step.Status switch
        {
            TransitionStepStatus.Running => ("⏳", _theme.Accent, _theme.Text, step.Description),
            TransitionStepStatus.Done => ("✅", Color.FromArgb(80, 200, 120), _theme.Text, step.StatusMessage),
            TransitionStepStatus.Warning => ("⚠️", Color.FromArgb(220, 170, 60), _theme.Text, step.StatusMessage),
            TransitionStepStatus.Failed => ("❌", Color.FromArgb(220, 80, 80), Color.FromArgb(220, 80, 80), step.StatusMessage),
            TransitionStepStatus.Skipped => ("⏭️", _theme.Muted, _theme.Muted, step.StatusMessage),
            _ => ("⬜", _theme.Muted, _theme.Text, step.Description)
        };
    }

    private async Task RunStepsAsync()
    {
        if (_running) return;

        _running = true;
        _actionButton.Text = "Cancel";
        _summaryLabel.Text = "Applying changes…";
        _summaryLabel.ForeColor = _theme.Text;

        bool anyFailed = false;
        bool anyWarning = false;

        for (int i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            if (step.Execute == null)
            {
                step.Status = TransitionStepStatus.Skipped;
                step.StatusMessage = "No action required.";
                UpdateRow(i);
                continue;
            }

            step.Status = TransitionStepStatus.Running;
            UpdateRow(i);
            _stepListPanel.Refresh();

            try
            {
                var (success, message, isWarning) = await step.Execute();
                step.StatusMessage = message;
                if (!success)
                {
                    step.Status = TransitionStepStatus.Failed;
                    anyFailed = true;
                }
                else if (isWarning)
                {
                    step.Status = TransitionStepStatus.Warning;
                    anyWarning = true;
                }
                else
                {
                    step.Status = TransitionStepStatus.Done;
                }
            }
            catch (Exception ex)
            {
                step.Status = TransitionStepStatus.Failed;
                step.StatusMessage = ex.Message;
                anyFailed = true;
            }

            UpdateRow(i);

            if (anyFailed)
                break;
        }

        _running = false;

        if (anyFailed)
        {
            _rollbackRequired = _steps.Any(step => step.Status is TransitionStepStatus.Done or TransitionStepStatus.Warning);
            _summaryLabel.Text = _rollbackRequired
                ? "A step failed. Click Cancel to roll back completed changes."
                : "A step failed. No changes were committed.";
            _summaryLabel.ForeColor = Color.FromArgb(220, 80, 80);
            _actionButton.Text = "Cancel";
            return;
        }

        TransitionCompleted = true;
        _actionButton.Text = "Close";

        if (anyWarning)
        {
            _summaryLabel.Text = "Done — with warnings. Review the items above.";
            _summaryLabel.ForeColor = Color.FromArgb(220, 170, 60);
            return;
        }

        _summaryLabel.Text = "All steps completed successfully.";
        _summaryLabel.ForeColor = Color.FromArgb(80, 200, 120);
        await Task.Delay(1500);
        if (!IsDisposed)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private async Task RollbackAsync()
    {
        for (int i = _steps.Count - 1; i >= 0; i--)
        {
            var step = _steps[i];
            if (step.Status is not (TransitionStepStatus.Done or TransitionStepStatus.Warning) || step.Rollback == null)
                continue;

            try { await step.Rollback(); } catch { }
        }
    }

    private async void ActionButton_Click(object? sender, EventArgs e)
    {
        if (_running) return;

        if (_rollbackRequired)
        {
            _actionButton.Enabled = false;
            _summaryLabel.Text = "Rolling back completed changes…";
            await RollbackAsync();
            _rollbackRequired = false;
            _summaryLabel.Text = "Rollback complete. No changes were saved.";
            _actionButton.Enabled = true;
        }

        DialogResult = TransitionCompleted ? DialogResult.OK : DialogResult.Cancel;
        Close();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && !_running)
        {
            DialogResult = TransitionCompleted ? DialogResult.OK : DialogResult.Cancel;
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
