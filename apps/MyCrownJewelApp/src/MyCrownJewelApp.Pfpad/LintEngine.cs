using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyCrownJewelApp.Pfpad.Roslyn;

namespace MyCrownJewelApp.Pfpad;

public sealed class LintEngine : IDisposable
{
    // Immutable snapshot replaced atomically; background threads read via local variable.
    private volatile List<LintRule> _rules;
    private CancellationTokenSource? _cts;
    private bool _enabled = true;
    private bool _sastEnabled = true;
    private readonly System.Windows.Forms.Timer _debounceTimer;
    private IRoslynWorkspace? _roslynWorkspace;

    // Pending work written on UI thread, read on timer tick (also UI thread → Task.Run).
    private string _pendingText = "";
    private volatile string _pendingFilePath = "";

    // Captured at construction time (must be created on the UI thread).
    private readonly SynchronizationContext? _uiContext;

    public event Action<IReadOnlyList<Diagnostic>>? DiagnosticsUpdated;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                _debounceTimer.Stop();
                _cts?.Cancel();
                DiagnosticsUpdated?.Invoke([]);
            }
        }
    }

    public bool SastEnabled { get => _sastEnabled; set => _sastEnabled = value; }

    public bool HighlightHardcodedSecrets { get; set; } = true;

    public LintEngine()
    {
        _uiContext = SynchronizationContext.Current;
        _rules = BuildRules(120, flagMagicNumbers: true, flagNamingConventions: true, flagSecrets: true);

        _debounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _debounceTimer.Tick += OnDebounceElapsed;
    }

    /// <summary>Reconfigures the lint engine at runtime; rebuilds the rule list immediately.</summary>
    public void Configure(bool enabled, int maxLineLength, bool flagMagicNumbers, bool flagNamingConventions)
    {
        // Atomically replace the rule list so the background thread always sees a consistent snapshot.
        _rules = BuildRules(Math.Clamp(maxLineLength, 60, 300), flagMagicNumbers, flagNamingConventions, HighlightHardcodedSecrets);
        Enabled = enabled;
    }

    private List<LintRule> BuildRules(int maxLineLength, bool flagMagicNumbers, bool flagNamingConventions, bool flagSecrets)
    {
        var rules = new List<LintRule>
        {
            new TrailingWhitespaceRule(),
            new LineTooLongRule(maxLineLength),
            new MissingSemicolonRule(),
        };
        if (flagMagicNumbers)      rules.Add(new MagicNumberRule());
        if (flagNamingConventions) rules.Add(new NamingConventionRule());
        if (flagSecrets)           rules.Add(new HardcodedSecretsRule());
        return rules;
    }

    /// <summary>
    /// Schedules a lint run for the given text. Calls are debounced — only the last call
    /// within a 400 ms window triggers actual analysis. Safe to call on every keystroke.
    /// </summary>
    public void ScheduleLint(string text, string filePath)
    {
        if (!_enabled) return;

        // Store pending work; the timer tick will pick it up.
        _pendingText = text;
        _pendingFilePath = filePath;

        // Restart the debounce timer (coalesces rapid calls into one).
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnDebounceElapsed(object? sender, EventArgs e)
    {
        _debounceTimer.Stop();
        if (!_enabled) return;

        // Capture state on the UI thread before handing off to the thread pool.
        var text       = _pendingText;
        var filePath   = _pendingFilePath;
        var rules      = _rules;           // volatile read → snapshot; background task owns this reference
        var workspace  = _roslynWorkspace;
        var uiContext  = _uiContext;

        _cts?.Cancel();
        var cts   = new CancellationTokenSource();
        _cts      = cts;
        var token = cts.Token;

        bool useRoslyn = _sastEnabled && workspace is not null &&
            filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

        _ = Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested();

                List<Diagnostic> diags;

                if (useRoslyn)
                {
                    diags = await GetRoslynDiagnosticsAsync(workspace!, filePath, token).ConfigureAwait(false);
                }
                else
                {
                    diags = [];
                    foreach (var rule in rules)
                    {
                        token.ThrowIfCancellationRequested();
                        try { rule.Analyze(text, filePath, diags); }
                        catch { }
                    }
                }

                token.ThrowIfCancellationRequested();

                diags.Sort(static (a, b) =>
                {
                    int c = a.Line.CompareTo(b.Line);
                    return c != 0 ? c : a.Column.CompareTo(b.Column);
                });

                IReadOnlyList<Diagnostic> snapshot = diags.AsReadOnly();

                if (uiContext is not null)
                    uiContext.Post(_ => DiagnosticsUpdated?.Invoke(snapshot), null);
                else
                    DiagnosticsUpdated?.Invoke(snapshot);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (Interlocked.CompareExchange(ref _cts, null, cts) == cts)
                    cts.Dispose();
            }
        }, token);
    }

    public void SetRoslynWorkspace(IRoslynWorkspace workspace)
    {
        _roslynWorkspace = workspace;
    }

    private static async Task<List<Diagnostic>> GetRoslynDiagnosticsAsync(
        IRoslynWorkspace workspace, string filePath, CancellationToken ct)
    {
        try
        {
            var roslynDiags = await workspace.GetRoslynDiagnosticsAsync().ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return roslynDiags
                .Where(d => d.Severity is Microsoft.CodeAnalysis.DiagnosticSeverity.Error
                                      or Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                .Select(d => new Diagnostic
                {
                    File     = filePath,
                    Line     = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column   = d.Location.GetLineSpan().StartLinePosition.Character + 1,
                    Length   = d.Location.SourceSpan.Length,
                    Message  = d.GetMessage(),
                    Severity = d.Severity switch
                    {
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Error   => DiagnosticSeverity.Error,
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
                        Microsoft.CodeAnalysis.DiagnosticSeverity.Info    => DiagnosticSeverity.Suggestion,
                        _                                                  => DiagnosticSeverity.Hint
                    },
                    RuleId = d.Id
                })
                .ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    public void Dispose()
    {
        _debounceTimer.Stop();
        _debounceTimer.Tick -= OnDebounceElapsed;
        _debounceTimer.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
