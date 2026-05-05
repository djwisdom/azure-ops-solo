using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

public sealed class LintEngine : IDisposable
{
    private readonly List<LintRule> _rules;
    private CancellationTokenSource? _cts;
    private bool _enabled = true;
    private System.Windows.Forms.Timer? _debounceTimer;

    public event Action<IReadOnlyList<Diagnostic>>? DiagnosticsUpdated;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                _cts?.Cancel();
                DiagnosticsUpdated?.Invoke(Array.Empty<Diagnostic>());
            }
        }
    }

    public LintEngine()
    {
        _rules = new List<LintRule>
        {
            new TrailingWhitespaceRule(),
            new LineTooLongRule(120),
            new MagicNumberRule(),
            new MissingSemicolonRule(),
            new NamingConventionRule()
        };

        _debounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            // Run is triggered externally via ScheduleLint
        };
    }

    public void ScheduleLint(string text, string filePath)
    {
        if (!_enabled) return;
        _debounceTimer?.Stop();
        _debounceTimer?.Start();

        // Cancel previous pending lint
        _cts?.Cancel();

        var cts = new CancellationTokenSource();
        _cts = cts;
        var token = cts.Token;

        Task.Run(() =>
        {
            try
            {
                if (token.IsCancellationRequested) return;

                var diags = new List<Diagnostic>();
                foreach (var rule in _rules)
                {
                    if (token.IsCancellationRequested) return;
                    try { rule.Analyze(text, filePath, diags); }
                    catch { }
                }

                if (token.IsCancellationRequested) return;

                // Sort by line then column
                diags.Sort((a, b) =>
                {
                    int c = a.Line.CompareTo(b.Line);
                    return c != 0 ? c : a.Column.CompareTo(b.Column);
                });

                var snapshot = diags.AsReadOnly();
                try
                {
                    SynchronizationContext.Current?.Post(_ =>
                    {
                        DiagnosticsUpdated?.Invoke(snapshot);
                    }, null);
                }
                catch { }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (_cts == cts)
                {
                    cts.Dispose();
                    _cts = null;
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
    }
}
