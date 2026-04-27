using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using System.Text;

namespace MyCrownJewelApp.Terminal;

/// <summary>
/// TerminalPane is an Avalonia UserControl that hosts a terminal session.
/// Wraps Avalonia.TerminalControl.TerminalControl and connects it to a TerminalManager.
/// </summary>
public partial class TerminalPane : UserControl, IDisposable
{
    private TerminalManager? _terminalManager;
    private bool _disposed;

    public TerminalPane()
    {
        // Load XAML definition
        AvaloniaXamlLoader.Load(this);
        InitializeComponent();
        InitializeTerminal();
    }

    private void InitializeComponent()
    {
        // After XAML load, TerminalWidget is available
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Start terminal when control is loaded into visual tree
        _terminalManager?.Dispose();
        _terminalManager = new TerminalManager();
        _terminalManager.OutputReceived += OnOutputReceived;
        _terminalManager.ErrorReceived += OnErrorReceived;
        _terminalManager.CreateTerminal();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private void OnOutputReceived(string data)
    {
        // Write terminal output to the UI control on UI thread
        Dispatcher.UIThread.Post(() =>
        {
            TerminalWidget?.Write(data);
        });
    }

    private void OnErrorReceived(string data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TerminalWidget?.Write(data);
        });
    }

    /// <summary>
    /// Sends raw input to the terminal (e.g., a command followed by Enter).
    /// </summary>
    public void SendInput(string input)
    {
        _terminalManager?.SendInput(input);
    }

    /// <summary>
    /// Clears the terminal screen.
    /// </summary>
    public void Clear()
    {
        Dispatcher.UIThread.Post(() => TerminalWidget?.Clear());
    }

    /// <summary>
    /// Gets the accumulated terminal output.
    /// </summary>
    public string Output => _terminalManager?.Output ?? string.Empty;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _terminalManager?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
