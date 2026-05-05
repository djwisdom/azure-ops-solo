using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyCrownJewelApp.Pfpad.Debugger;

public static class Dap
{
    public record Message
    {
        [JsonPropertyOrder(0)] public string? Type { get; init; }
        [JsonPropertyOrder(1)] public int? Seq { get; init; }
    }

    public record Request : Message
    {
        public string Command { get; init; } = "";
        public JsonElement? Arguments { get; init; }
    }

    public record Response : Message
    {
        public int RequestSeq { get; init; }
        public bool Success { get; init; }
        public string? Command { get; init; }
        public string? Message { get; init; }
        public JsonElement? Body { get; init; }
    }

    public record Event : Message
    {
        public string EventType { get; init; } = "";
        public JsonElement? Body { get; init; }
    }

    public record Capabilities
    {
        public bool SupportsConfigurationDoneRequest { get; init; }
        public bool SupportsSetVariable { get; init; }
        public bool SupportsConditionalBreakpoints { get; init; }
        public bool SupportsLogPoints { get; init; }
        public bool SupportsHitConditionalBreakpoints { get; init; }
        public bool SupportsFunctionBreakpoints { get; init; }
        public bool SupportsEvaluateForHovers { get; init; }
    }

    public record StackFrame
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public Source? Source { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }
        public int? EndLine { get; init; }
    }

    public record Source
    {
        public string? Name { get; init; }
        public string? Path { get; init; }
        public int? SourceReference { get; init; }
    }

    public record Scope
    {
        public string Name { get; init; } = "";
        public int VariablesReference { get; init; }
        public bool Expensive { get; init; }
    }

    public record Variable
    {
        public string Name { get; init; } = "";
        public string Value { get; init; } = "";
        public string? Type { get; init; }
        public int VariablesReference { get; init; }
        public int? NamedVariables { get; init; }
    }

    public record Thread
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
    }

    public record Breakpoint
    {
        public int? Id { get; init; }
        public bool Verified { get; init; }
        public int? Line { get; init; }
        public string? Message { get; init; }
        public Source? Source { get; init; }
    }

    public record StoppedEventBody
    {
        public string Reason { get; init; } = "";
        public int? ThreadId { get; init; }
        public string? Description { get; init; }
        public string? Text { get; init; }
        public bool AllThreadsStopped { get; init; }
        public int? HitBreakpointIds { get; init; }
    }

    public record ContinuedEventBody
    {
        public int ThreadId { get; init; }
        public bool AllThreadsContinued { get; init; }
    }

    public record OutputEventBody
    {
        public string Category { get; init; } = "";
        public string Output { get; init; } = "";
    }

    public record ExitedEventBody
    {
        public int ExitCode { get; init; }
    }

    public record TerminatedEventBody
    {
        public int? Restart { get; init; }
    }

    public record BreakpointEventBody
    {
        public string Reason { get; init; } = "";
        public Breakpoint? Breakpoint { get; init; }
    }

    public record ProcessEventBody
    {
        public string Name { get; init; } = "";
        public int? SystemProcessId { get; init; }
    }

    public record StackTraceResponseBody
    {
        public StackFrame[]? StackFrames { get; init; }
        public int? TotalFrames { get; init; }
    }

    public record ScopesResponseBody
    {
        public Scope[]? Scopes { get; init; }
    }

    public record VariablesResponseBody
    {
        public Variable[]? Variables { get; init; }
    }

    public record EvaluateResponseBody
    {
        public string Result { get; init; } = "";
        public string? Type { get; init; }
        public int VariablesReference { get; init; }
        public int? NamedVariables { get; init; }
        public int? IndexedVariables { get; init; }
    }

    public record SetBreakpointsResponseBody
    {
        public Breakpoint[]? Breakpoints { get; init; }
    }

    public record ThreadsResponseBody
    {
        public Thread[]? Threads { get; init; }
    }

    public record SourceResponseBody
    {
        public string? Content { get; init; }
        public string? MimeType { get; init; }
    }

    public record InitializeRequestArguments
    {
        public string ClientId { get; init; } = "pfpad";
        public string ClientName { get; init; } = "Personal Flip Pad";
        public string AdapterId { get; init; } = "netcoredbg";
        public bool Locale { get; init; }
        public bool LinesStartAt1 { get; init; } = true;
        public bool ColumnsStartAt1 { get; init; } = true;
        public bool SupportsVariableType { get; init; } = true;
        public bool SupportsVariablePaging { get; init; }
        public bool SupportsRunInTerminalRequest { get; init; }
    }

    public record LaunchRequestArguments
    {
        public string Program { get; init; } = "";
        public string? Cwd { get; init; }
        public string[]? Args { get; init; }
        public string? Type { get; init; } = "coreclr";
        public bool? Console { get; init; }
        public bool? StopAtEntry { get; init; }
        public string? Env { get; init; }
        public string? PipeTransport { get; init; }
    }

    public record SetBreakpointsArguments
    {
        public Source? Source { get; init; }
        public SourceBreakpoint[]? Breakpoints { get; init; }
        public bool? SourceModified { get; init; }
    }

    public record SourceBreakpoint
    {
        public int Line { get; init; }
        public int? Column { get; init; }
        public string? Condition { get; init; }
        public string? HitCondition { get; init; }
        public string? LogMessage { get; init; }
    }

    public record StackTraceArguments
    {
        public int ThreadId { get; init; }
        public int? StartFrame { get; init; }
        public int? Levels { get; init; }
    }

    public record ScopesArguments
    {
        public int FrameId { get; init; }
    }

    public record VariablesArguments
    {
        public int VariablesReference { get; init; }
    }

    public record EvaluateArguments
    {
        public string Expression { get; init; } = "";
        public int? FrameId { get; init; }
        public string? Context { get; init; }
    }

    public record NextArguments
    {
        public int ThreadId { get; init; }
    }

    public record StepInArguments
    {
        public int ThreadId { get; init; }
    }

    public record StepOutArguments
    {
        public int ThreadId { get; init; }
    }

    public record ContinueArguments
    {
        public int ThreadId { get; init; }
    }

    public record PauseArguments
    {
        public int ThreadId { get; init; }
    }

    public static JsonSerializerOptions JsonOpts => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
