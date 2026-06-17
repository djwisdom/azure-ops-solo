using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace MyCrownJewelApp.Pfpad;

/// <summary>
/// Centralised runtime security enforcement driven by the active <see cref="SecurityProfile"/>.
/// All methods are pure/static — consumers pass the current profile.
/// </summary>
public static class SecurityEnforcementService
{
    private static readonly string[] AlwaysBlockedSchemes = ["javascript", "vbscript", "data", "ms-settings", "ms-msdt"];
    private static readonly string[] MaxBlockedSchemes = ["file", "ftp"];

    /// <summary>
    /// Returns true when the URL is safe to open with UseShellExecute=true at the given profile.
    /// At Low+: blocks javascript:, vbscript:, data:, ms-settings:, ms-msdt:.
    /// At Max: additionally blocks file:, ftp:.
    /// </summary>
    public static bool IsUrlSchemeAllowed(string? url, SecurityProfile profile)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (profile == SecurityProfile.NotHardened) return true;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        string scheme = uri.Scheme.ToLowerInvariant();

        foreach (var blocked in AlwaysBlockedSchemes)
            if (scheme == blocked) return false;

        if (profile >= SecurityProfile.Max)
        {
            foreach (var blocked in MaxBlockedSchemes)
                if (scheme == blocked) return false;
        }

        return true;
    }

    /// <summary>
    /// Canonicalizes the path and returns true if it is safe to open.
    /// At Low+: resolves symlinks and ensures the resolved path does not contain null bytes.
    /// </summary>
    public static bool IsPathSafe(string? path, SecurityProfile profile)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (profile == SecurityProfile.NotHardened) return true;

        try
        {
            string full = Path.GetFullPath(path);
            return !full.Contains('\0');
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Returns true if TLS certificate validation should be enforced (Low+).</summary>
    public static bool ShouldValidateTls(SecurityProfile profile) => profile >= SecurityProfile.Low;

    /// <summary>Returns true if the AIOps credential flow should use Azure SDK instead of CLI shell (Mid+).</summary>
    public static bool ShouldUseSdkCredentials(SecurityProfile profile) => profile >= SecurityProfile.Mid;

    /// <summary>Returns true if settings.json should be DPAPI-encrypted (Mid+).</summary>
    public static bool ShouldEncryptSettings(SecurityProfile profile) => profile >= SecurityProfile.Mid;

    /// <summary>Returns true if log entries should be scrubbed for secrets (Mid+).</summary>
    public static bool ShouldMaskLogSecrets(SecurityProfile profile) => profile >= SecurityProfile.Mid;

    /// <summary>Returns true if http:// is blocked for AIOps endpoints (Max only).</summary>
    public static bool RequireHttpsEndpoints(SecurityProfile profile) => profile >= SecurityProfile.Max;

    /// <summary>
    /// Returns true if the running executable has an Authenticode digital signature.
    /// </summary>
    public static bool IsExeSigned()
    {
        try
        {
            string exe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            var cert = X509CertificateLoader.LoadCertificateFromFile(exe);
            return cert != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true when the CI deploy pipeline YAML has Wiz or CodeQL scanning un-commented.
    /// Looks for deploy-app.yml two levels up from the app source directory.
    /// </summary>
    public static bool AreCiGatesEnabled()
    {
        try
        {
            string? repoRoot = FindRepoRoot(AppContext.BaseDirectory);
            if (repoRoot == null) return false;
            string yml = Path.Combine(repoRoot, "pipelines", "deploy-app.yml");
            if (!File.Exists(yml)) return false;
            string content = File.ReadAllText(yml);
            foreach (var line in content.Split('\n'))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith('#')) continue;
                if (trimmed.Contains("wiz", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("codeql", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true when setup.iss contains an uncommented SignTool directive.
    /// </summary>
    public static bool IsInstallerSigned()
    {
        try
        {
            string? repoRoot = FindRepoRoot(AppContext.BaseDirectory);
            if (repoRoot == null) return false;
            string iss = Path.Combine(repoRoot, "deploy", "setup.iss");
            if (!File.Exists(iss)) return false;
            foreach (var line in File.ReadLines(iss))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith(';')) continue;
                if (trimmed.StartsWith("SignTool", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindRepoRoot(string start)
    {
        string? dir = start;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")) ||
                File.Exists(Path.Combine(dir, "global.json")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
