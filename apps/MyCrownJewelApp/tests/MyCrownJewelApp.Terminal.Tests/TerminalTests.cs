using MyCrownJewelApp.Terminal;
using Xunit;

namespace MyCrownJewelApp.Terminal.Tests;

/// <summary>
/// Basic integration tests for TerminalManager.
/// These tests require a shell (cmd/bash/zsh) available on the system.
/// They spawn a terminal process and verify that echo produces expected output.
/// </summary>
public class TerminalTests : IDisposable
{
    private readonly TerminalManager _terminal;

    public TerminalTests()
    {
        _terminal = new TerminalManager();
        _terminal.CreateTerminal();
    }

    [Fact]
    public void Echo_Command_ProducesExpectedOutput()
    {
        // Arrange
        string command = "echo test";
        _terminal.ClearBuffers();

        // Act
        _terminal.SendInput(command + "\n");

        // Wait briefly for output to arrive (async). In production use proper sync.
        Thread.Sleep(500);

        // Assert
        string output = _terminal.Output;
        Assert.Contains("test", output);
    }

    [Fact]
    public void MultipleEchos_AccumulateInBuffer()
    {
        // Arrange
        _terminal.ClearBuffers();

        // Act
        _terminal.SendInput("echo first\n");
        Thread.Sleep(300);
        _terminal.SendInput("echo second\n");
        Thread.Sleep(300);

        // Assert
        string output = _terminal.Output;
        Assert.Contains("first", output);
        Assert.Contains("second", output);
    }

    public void Dispose()
    {
        _terminal.Dispose();
    }
}
