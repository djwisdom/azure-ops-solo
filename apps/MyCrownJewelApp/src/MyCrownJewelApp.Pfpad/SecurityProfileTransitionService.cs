using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MyCrownJewelApp.Pfpad;

public enum TransitionStepStatus { Pending, Running, Done, Warning, Skipped, Failed }

public sealed class TransitionStep
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public TransitionStepStatus Status { get; set; } = TransitionStepStatus.Pending;
    public string StatusMessage { get; set; } = "";
    public Func<Task<(bool success, string message, bool isWarning)>>? Execute { get; init; }
    public Func<Task>? Rollback { get; init; }
}

/// <summary>
/// Builds and executes ordered migration steps when the security profile changes.
/// User-centric: steps are auto-remediating, failures are non-blocking warnings where possible,
/// and all file mutations are reversible.
/// </summary>
public sealed class SecurityProfileTransitionService
{
    private readonly SettingsService _settingsService;

    public SecurityProfileTransitionService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>Returns the ordered steps needed to transition from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public List<TransitionStep> GetSteps(SecurityProfile from, SecurityProfile to)
    {
        var steps = new List<TransitionStep>();

        bool fromNeedsEncryption = from >= SecurityProfile.Mid;
        bool toNeedsEncryption = to >= SecurityProfile.Mid;

        if (!fromNeedsEncryption && toNeedsEncryption)
        {
            steps.Add(new TransitionStep
            {
                Title = "Back up settings.json",
                Description = "Creates settings.json.bak so you can restore the original if needed.",
                Execute = async () =>
                {
                    await Task.Yield();
                    string src = _settingsService.SettingsFilePath;
                    if (!File.Exists(src)) return (true, "No settings file yet — nothing to back up.", false);
                    string bak = src + ".bak";
                    File.Copy(src, bak, overwrite: true);
                    return (true, $"Backed up to {Path.GetFileName(bak)}", false);
                }
            });

            steps.Add(new TransitionStep
            {
                Title = "Encrypt settings.json with Windows DPAPI",
                Description = "Protects your settings with your Windows user account. Only you can decrypt it on this machine.",
                Execute = async () =>
                {
                    await Task.Yield();
                    var current = _settingsService.LoadWithDecrypt();
                    if (current == null) return (true, "No settings to encrypt — will encrypt on next save.", false);
                    try
                    {
                        _settingsService.Save(current, encrypt: true);
                        return (true, "settings.json is now DPAPI-encrypted.", false);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Encryption failed: {ex.Message}. Settings remain plain-text.", true);
                    }
                },
                Rollback = async () =>
                {
                    await Task.Yield();
                    string src = _settingsService.SettingsFilePath;
                    string bak = src + ".bak";
                    if (File.Exists(bak))
                        File.Copy(bak, src, overwrite: true);
                }
            });
        }
        else if (fromNeedsEncryption && !toNeedsEncryption)
        {
            steps.Add(new TransitionStep
            {
                Title = "Decrypt settings.json",
                Description = "Converts settings back to plain JSON. Creates settings.json.enc.bak first.",
                Execute = async () =>
                {
                    await Task.Yield();
                    string src = _settingsService.SettingsFilePath;
                    if (!File.Exists(src)) return (true, "No settings file — nothing to decrypt.", false);

                    string encBak = src + ".enc.bak";
                    try { File.Copy(src, encBak, overwrite: true); } catch { }

                    var settings = _settingsService.LoadWithDecrypt();
                    if (settings == null)
                        return (false, "Could not read settings (corrupted?). Resetting to defaults on next launch.", true);

                    try
                    {
                        _settingsService.Save(settings, encrypt: false);
                        return (true, "settings.json restored to plain JSON.", false);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Decryption write failed: {ex.Message}", true);
                    }
                }
            });
        }

        if (from < SecurityProfile.Low && to >= SecurityProfile.Low)
        {
            steps.Add(new TransitionStep
            {
                Title = "Enable TLS certificate validation",
                Description = "Kubernetes and network connectors will verify TLS certificates.",
                Execute = async () =>
                {
                    await Task.Yield();
                    return await TryRemediateTlsSettingsAsync(to);
                }
            });

            steps.Add(new TransitionStep
            {
                Title = "Enable URL scheme validation",
                Description = "Dangerous URI schemes (javascript:, vbscript:, ms-msdt:) will be blocked in all link actions.",
                Execute = async () =>
                {
                    await Task.Yield();
                    return (true, "URL scheme allowlist active.", false);
                }
            });
        }

        if (to == SecurityProfile.NotHardened)
        {
            steps.Add(new TransitionStep
            {
                Title = "⚠️  Security protections removed",
                Description = "All runtime enforcement is now off. Dangerous URI schemes and unvalidated paths will no longer be blocked.",
                Execute = async () =>
                {
                    await Task.Yield();
                    return (true, "No enforcement active. Use for development only.", true);
                }
            });
        }

        if (from < SecurityProfile.Max && to >= SecurityProfile.Max)
        {
            steps.Add(new TransitionStep
            {
                Title = "⚠️  http:// links will be blocked",
                Description = "At Max, plain http:// URLs in notifications, terminal output and AIOps panels will be silently blocked. Use https:// endpoints.",
                Execute = async () =>
                {
                    await Task.Yield();
                    return (true, "Strict URL allowlist active. Switch to https:// for any affected endpoints.", true);
                }
            });
        }

        if (from >= SecurityProfile.Max && to < SecurityProfile.Max)
        {
            steps.Add(new TransitionStep
            {
                Title = "Restore http:// link support",
                Description = "Plain http:// links in notifications and terminal output will open again.",
                Execute = async () =>
                {
                    await Task.Yield();
                    return (true, "http:// links restored.", false);
                }
            });
        }

        steps.Add(new TransitionStep
        {
            Title = "Apply new security profile",
            Description = $"Security profile set to {to}.",
            Execute = async () =>
            {
                await Task.Yield();
                SecurityEnforcementService.CurrentProfile = to;
                return (true, $"Profile is now {to}.", false);
            }
        });

        return steps;
    }

    private async Task<(bool success, string message, bool isWarning)> TryRemediateTlsSettingsAsync(SecurityProfile targetProfile)
    {
        await Task.Yield();

        try
        {
            var current = _settingsService.LoadWithDecrypt();
            if (current?.AIOpsConfig?.Kubernetes == null)
                return (true, "TLS validation preference set.", false);

            if (!current.AIOpsConfig.Kubernetes.SkipTlsVerify)
                return (true, "TLS validation already enabled for Kubernetes.", false);

            var updatedAiOps = current.AIOpsConfig.CreateEncryptedCopy();
            updatedAiOps.LoadSecretsFromEncrypted();
            updatedAiOps.Kubernetes.SkipTlsVerify = false;

            var updatedSettings = current with { AIOpsConfig = updatedAiOps };
            _settingsService.Save(updatedSettings, SecurityEnforcementService.ShouldEncryptSettings(targetProfile));
            return (true, "TLS validation enabled and Kubernetes SkipTlsVerify was turned off.", false);
        }
        catch (Exception ex)
        {
            return (true, $"TLS validation will be enforced, but Kubernetes TLS remediation needs manual review: {ex.Message}", true);
        }
    }
}
