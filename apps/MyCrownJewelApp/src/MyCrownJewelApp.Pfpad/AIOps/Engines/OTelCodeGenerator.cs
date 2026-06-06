namespace MyCrownJewelApp.Pfpad.AIOps;

public class OTelCodeGenerator
{
    public string GenerateForFunction(string language, string functionName, string? serviceName = null)
        => NormalizeLanguage(language) switch
        {
            "python" => GeneratePythonFunction(functionName, serviceName),
            "javascript" => GenerateJavaScriptFunction(functionName, serviceName, false),
            "typescript" => GenerateJavaScriptFunction(functionName, serviceName, true),
            "go" => GenerateGoFunction(functionName, serviceName),
            "java" => GenerateJavaFunction(functionName, serviceName),
            _ => GenerateCSharpFunction(functionName, serviceName)
        };

    public string GenerateForClass(string language, string className, string? serviceName = null)
        => NormalizeLanguage(language) switch
        {
            "python" => GeneratePythonClass(className, serviceName),
            "javascript" => GenerateJavaScriptClass(className, serviceName, false),
            "typescript" => GenerateJavaScriptClass(className, serviceName, true),
            "go" => GenerateGoClass(className, serviceName),
            "java" => GenerateJavaClass(className, serviceName),
            _ => GenerateCSharpClass(className, serviceName)
        };

    private static string NormalizeLanguage(string language)
    {
        string normalized = (language ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        return normalized switch
        {
            "cs" or "c#" or "csharp" => "csharp",
            "py" or "python" => "python",
            "js" or "javascript" => "javascript",
            "ts" or "typescript" => "typescript",
            "golang" or "go" => "go",
            "java" => "java",
            _ => "csharp"
        };
    }

    private static string ServiceNameOrDefault(string? serviceName) => string.IsNullOrWhiteSpace(serviceName) ? "pfpad-service" : serviceName;

    private static string GenerateCSharpFunction(string functionName, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "using System.Diagnostics;",
            string.Empty,
            $"private static readonly ActivitySource ActivitySource = new(\"{svc}\");",
            string.Empty,
            $"public async Task {functionName}Async(CancellationToken ct)",
            "{",
            $"    using var activity = ActivitySource.StartActivity(\"{functionName}\", ActivityKind.Internal);",
            $"    activity?.SetTag(\"service.name\", \"{svc}\");",
            $"    activity?.SetTag(\"code.function\", \"{functionName}\");",
            string.Empty,
            "    try",
            "    {",
            "        await Task.CompletedTask;",
            "        activity?.SetStatus(ActivityStatusCode.Ok);",
            "    }",
            "    catch (Exception ex)",
            "    {",
            "        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);",
            "        activity?.RecordException(ex);",
            "        throw;",
            "    }",
            "}");
    }

    private static string GenerateCSharpClass(string className, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "using System.Diagnostics;",
            string.Empty,
            $"public sealed class {className}",
            "{",
            $"    private static readonly ActivitySource ActivitySource = new(\"{svc}\");",
            string.Empty,
            "    public void Execute(string input)",
            "    {",
            $"        using var activity = ActivitySource.StartActivity(\"{className}.Execute\", ActivityKind.Internal);",
            $"        activity?.SetTag(\"service.name\", \"{svc}\");",
            $"        activity?.SetTag(\"code.namespace\", \"{className}\");",
            "        activity?.SetTag(\"input.length\", input?.Length ?? 0);",
            string.Empty,
            "        try",
            "        {",
            "            activity?.SetStatus(ActivityStatusCode.Ok);",
            "        }",
            "        catch (Exception ex)",
            "        {",
            "            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);",
            "            activity?.RecordException(ex);",
            "            throw;",
            "        }",
            "    }",
            "}");
    }

    private static string GeneratePythonFunction(string functionName, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "from opentelemetry import trace",
            "from opentelemetry.trace import Status, StatusCode",
            string.Empty,
            $"tracer = trace.get_tracer(\"{svc}\")",
            string.Empty,
            $"async def {functionName}(ctx):",
            $"    with tracer.start_as_current_span(\"{functionName}\") as span:",
            $"        span.set_attribute(\"service.name\", \"{svc}\")",
            $"        span.set_attribute(\"code.function\", \"{functionName}\")",
            "        try:",
            "            span.set_status(Status(StatusCode.OK))",
            "        except Exception as ex:",
            "            span.record_exception(ex)",
            "            span.set_status(Status(StatusCode.ERROR, str(ex)))",
            "            raise");
    }

    private static string GeneratePythonClass(string className, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "from opentelemetry import trace",
            "from opentelemetry.trace import Status, StatusCode",
            string.Empty,
            $"class {className}:",
            $"    _tracer = trace.get_tracer(\"{svc}\")",
            string.Empty,
            "    def execute(self, payload):",
            $"        with self._tracer.start_as_current_span(\"{className}.execute\") as span:",
            $"            span.set_attribute(\"service.name\", \"{svc}\")",
            $"            span.set_attribute(\"code.namespace\", \"{className}\")",
            "            try:",
            "                span.set_status(Status(StatusCode.OK))",
            "            except Exception as ex:",
            "                span.record_exception(ex)",
            "                span.set_status(Status(StatusCode.ERROR, str(ex)))",
            "                raise");
    }

    private static string GenerateJavaScriptFunction(string functionName, string? serviceName, bool typeScript)
    {
        string svc = ServiceNameOrDefault(serviceName);
        string typeAnnotation = typeScript ? ": Promise<void>" : string.Empty;
        string payloadType = typeScript ? ": unknown" : string.Empty;
        return JoinLines(
            "const { trace, SpanStatusCode } = require('@opentelemetry/api');",
            string.Empty,
            $"const tracer = trace.getTracer('{svc}');",
            string.Empty,
            $"async function {functionName}(payload{payloadType}){typeAnnotation} {{",
            $"  const span = tracer.startSpan('{functionName}');",
            $"  span.setAttribute('service.name', '{svc}');",
            $"  span.setAttribute('code.function', '{functionName}');",
            string.Empty,
            "  try {",
            "    span.setStatus({ code: SpanStatusCode.OK });",
            "  } catch (error) {",
            "    span.recordException(error);",
            "    span.setStatus({ code: SpanStatusCode.ERROR, message: error instanceof Error ? error.message : String(error) });",
            "    throw error;",
            "  } finally {",
            "    span.end();",
            "  }",
            "}");
    }

    private static string GenerateJavaScriptClass(string className, string? serviceName, bool typeScript)
    {
        string svc = ServiceNameOrDefault(serviceName);
        string classKeyword = typeScript ? "export class" : "class";
        return JoinLines(
            "const { trace, SpanStatusCode } = require('@opentelemetry/api');",
            string.Empty,
            $"const tracer = trace.getTracer('{svc}');",
            string.Empty,
            $"{classKeyword} {className} {{",
            "  execute() {",
            $"    const span = tracer.startSpan('{className}.execute');",
            $"    span.setAttribute('service.name', '{svc}');",
            $"    span.setAttribute('code.namespace', '{className}');",
            string.Empty,
            "    try {",
            "      span.setStatus({ code: SpanStatusCode.OK });",
            "    } catch (error) {",
            "      span.recordException(error);",
            "      span.setStatus({ code: SpanStatusCode.ERROR, message: error instanceof Error ? error.message : String(error) });",
            "      throw error;",
            "    } finally {",
            "      span.end();",
            "    }",
            "  }",
            "}");
    }

    private static string GenerateGoFunction(string functionName, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "import (",
            "    \"context\"",
            string.Empty,
            "    \"go.opentelemetry.io/otel\"",
            "    \"go.opentelemetry.io/otel/codes\"",
            ")",
            string.Empty,
            $"var tracer = otel.Tracer(\"{svc}\")",
            string.Empty,
            $"func {functionName}(ctx context.Context) error {{",
            $"    ctx, span := tracer.Start(ctx, \"{functionName}\")",
            "    defer span.End()",
            string.Empty,
            "    if err := doWork(); err != nil {",
            "        span.RecordError(err)",
            "        span.SetStatus(codes.Error, err.Error())",
            "        return err",
            "    }",
            string.Empty,
            "    _ = ctx",
            "    span.SetStatus(codes.Ok, \"ok\")",
            "    return nil",
            "}");
    }

    private static string GenerateGoClass(string className, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "import (",
            "    \"context\"",
            string.Empty,
            "    \"go.opentelemetry.io/otel\"",
            "    \"go.opentelemetry.io/otel/codes\"",
            ")",
            string.Empty,
            $"type {className} struct{{}}",
            string.Empty,
            $"var tracer = otel.Tracer(\"{svc}\")",
            string.Empty,
            $"func (s *{className}) Execute(ctx context.Context) error {{",
            $"    ctx, span := tracer.Start(ctx, \"{className}.Execute\")",
            "    defer span.End()",
            string.Empty,
            "    if err := doWork(); err != nil {",
            "        span.RecordError(err)",
            "        span.SetStatus(codes.Error, err.Error())",
            "        return err",
            "    }",
            string.Empty,
            "    _ = ctx",
            "    span.SetStatus(codes.Ok, \"ok\")",
            "    return nil",
            "}");
    }

    private static string GenerateJavaFunction(string functionName, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "import io.opentelemetry.api.GlobalOpenTelemetry;",
            "import io.opentelemetry.api.trace.Span;",
            "import io.opentelemetry.api.trace.StatusCode;",
            "import io.opentelemetry.api.trace.Tracer;",
            string.Empty,
            $"private static final Tracer tracer = GlobalOpenTelemetry.getTracer(\"{svc}\");",
            string.Empty,
            $"public void {functionName}() {{",
            $"    Span span = tracer.spanBuilder(\"{functionName}\").startSpan();",
            $"    span.setAttribute(\"service.name\", \"{svc}\");",
            $"    span.setAttribute(\"code.function\", \"{functionName}\");",
            "    try {",
            "        span.setStatus(StatusCode.OK);",
            "    } catch (Exception ex) {",
            "        span.recordException(ex);",
            "        span.setStatus(StatusCode.ERROR, ex.getMessage());",
            "        throw ex;",
            "    } finally {",
            "        span.end();",
            "    }",
            "}");
    }

    private static string GenerateJavaClass(string className, string? serviceName)
    {
        string svc = ServiceNameOrDefault(serviceName);
        return JoinLines(
            "import io.opentelemetry.api.GlobalOpenTelemetry;",
            "import io.opentelemetry.api.trace.Span;",
            "import io.opentelemetry.api.trace.StatusCode;",
            "import io.opentelemetry.api.trace.Tracer;",
            string.Empty,
            $"public final class {className} {{",
            $"    private static final Tracer tracer = GlobalOpenTelemetry.getTracer(\"{svc}\");",
            string.Empty,
            "    public void execute() {",
            $"        Span span = tracer.spanBuilder(\"{className}.execute\").startSpan();",
            $"        span.setAttribute(\"service.name\", \"{svc}\");",
            $"        span.setAttribute(\"code.namespace\", \"{className}\");",
            "        try {",
            "            span.setStatus(StatusCode.OK);",
            "        } catch (Exception ex) {",
            "            span.recordException(ex);",
            "            span.setStatus(StatusCode.ERROR, ex.getMessage());",
            "            throw ex;",
            "        } finally {",
            "            span.end();",
            "        }",
            "    }",
            "}");
    }

    private static string JoinLines(params string[] lines) => string.Join(Environment.NewLine, lines);
}
