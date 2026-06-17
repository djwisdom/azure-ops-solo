# Security Hardening — Personal Flip Pad

**Settings → Security → Security Profile**

pfpad includes a built-in, graded runtime security hardening system. The hardening dial lets you
increase protections as your needs grow — from a frictionless development mode all the way to a
strict, compliance-ready environment. Every profile change is guided, reversible, and
auto-remediating: nothing happens silently.

---

## Profiles at a Glance

| Profile | Nickname | Best for |
|---------|----------|----------|
| **Not Hardened** | Dev mode | Exploring the app, debugging security rules, fully trusted local machines |
| **Low** | Baseline *(recommended default)* | Day-to-day professional use — zero friction, baseline protection |
| **Mid** | Hardened | Professional workstations, shared machines, production code |
| **Max** | Strict | High-security, regulated, or compliance-audited environments |

---

## Not Hardened

### Purpose

Disables all runtime security enforcement. Every URI scheme is permitted, all paths are
accepted as-is, and `settings.json` is plain-text JSON. Use this profile only when you need
maximum permissiveness for local experimentation or when debugging the security system itself.

### What is active

Nothing. Zero enforcement.

### Immediate results

- `javascript:`, `vbscript:`, `ms-msdt:`, `data:` URIs will execute if clicked.
- `file://` and `ftp://` links open without any validation.
- Settings file remains readable plain-text JSON.
- The transition wizard shows an explicit **⚠️ Security protections removed** warning step
  before applying the change, giving you a chance to cancel.

### When to use

- Local, single-user, fully trusted development machine.
- Temporarily, to verify a blocked link is a false positive.
- Never use when opening untrusted workspaces or connecting to external AIOps endpoints.

---

## Low (Recommended Default)

### Purpose

Provides baseline protection against the most commonly exploited attack vectors with
**zero practical friction** for normal use. All legitimate `http://`, `https://`, and `file://`
links continue to work. The only things blocked are URI schemes that have no legitimate use in
an editor and are routinely abused in social-engineering attacks.

### What is active

| Control | Detail |
|---------|--------|
| **URL scheme allowlist** | Blocks `javascript:`, `vbscript:`, `data:`, `ms-settings:`, `ms-msdt:` in all link actions |
| **TLS validation preference** | Kubernetes and network connectors prefer certificate validation |
| **File path canonicalization** | Paths resolved via `Path.GetFullPath()` before file operations |
| **Safe process launch** | Azure CLI calls use `ArgumentList` — no shell metacharacter injection possible |
| **Status-bar feedback** | Blocked links report in the status bar instead of silently failing |

### Immediate results

- Clicking a `javascript:` or `vbscript:` link (e.g., from terminal output or a notification)
  shows in the status bar: *"🔒 Link blocked (dangerous URI scheme). Change profile in
  Settings → Security."*
- Normal `http://`, `https://`, and `file://` links open as expected.
- AIOps connectors, Git operations, and all other features behave identically to Not Hardened.
- Settings remain readable plain-text JSON.

### Why choose Low

Almost identical day-to-day to Not Hardened. The blocked schemes (`javascript:`, `ms-msdt:`,
etc.) have caused real exploits in developer tools — blocking them costs nothing and prevents a
class of social-engineering attacks where a crafted workspace tricks the editor into executing
arbitrary commands.

---

## Mid

### Purpose

Hardens your persistent data and credential flows. Suitable for professional workstations,
shared machines, and anyone who wants to ensure their editor configuration — which can contain
API keys, tokens, workspace paths, and AIOps endpoint credentials — is protected from casual
inspection or exfiltration by other processes or users on the same machine.

### What is active (all Low controls, plus)

| Control | Detail |
|---------|--------|
| **DPAPI settings encryption** | `settings.json` is encrypted with your Windows user account via DPAPI |
| **SDK credential preference** | AIOps connectors prefer Azure SDK credential flows over raw CLI token shells |
| **Log secret masking** | Log entries are scrubbed to prevent secrets appearing in `startup.log` or `crash.log` |

### Immediate results

The **transition wizard** runs automatically when you switch to Mid:

1. **Backs up** `settings.json` → `settings.json.bak` in the same directory.
2. **Encrypts** `settings.json` with Windows DPAPI — the file is now protected binary data
   tied to your Windows user account on this machine.
3. Confirms success with a per-step status list and auto-closes after 1.5 seconds.

After transition:

- `settings.json` shows as binary in any text editor — it is not human-readable.
- **About → Open settings.json** detects the encryption and offers:
  *"Export plain JSON to Desktop for inspection?"* — export, review, then delete the copy.
- Downgrading back to Low runs the wizard in reverse: decrypts automatically
  (backed up as `settings.json.enc.bak`).
- If you move the file to a different machine or log in as a different Windows user,
  DPAPI decryption will fail; pfpad falls back to defaults and offers to reset.

### Why choose Mid

Prevents any other process running under your account — or any other user account on the
machine — from reading your API keys, tokens, and workspace configuration out of a plain-text
JSON file.

---

## Max

### Purpose

Adds strict network hygiene controls on top of Mid. Designed for environments where security
policy, compliance audits, or corporate governance require all network traffic to use encrypted
channels and no plaintext URIs to be opened from the editor surface.

### What is active (all Mid controls, plus)

| Control | Detail |
|---------|--------|
| **HTTPS-only AIOps endpoints** | Connectors refuse `http://` endpoints — all traffic must use TLS |
| **Strict URL allowlist** | `http://`, `file://`, and `ftp://` links blocked in all panels and notifications |

### Immediate results

The transition wizard warns you about the impact before applying. After transition:

- Clicking an `http://` link shows: *"🔒 Link blocked (http:// links are blocked at Max —
  use https://). Change profile in Settings → Security."*
- `file://` links are also blocked (e.g. clicking a local path in terminal output).
- AIOps connectors configured with `http://` endpoints will not connect — you must switch
  them to `https://` in AIOps Settings.
- `https://` links continue to open normally everywhere.

### Why choose Max

Satisfies "encrypt in transit" requirements in regulated environments. Combined with Mid's
at-rest DPAPI encryption, your configuration data is protected both in storage and over
the network. Suitable when a security posture review or compliance audit requires
demonstrable enforcement of encrypted-only traffic.

---

## Transition Wizard

When you change the Security Profile in **Settings → Security** and click **OK**, pfpad
shows a guided transition wizard **before** any change is applied.

### Wizard behaviour

```
┌─ Security Profile Change ─────────────────────────────────┐
│  ↑  Low  →  Mid                                           │
│  The following changes will be made to harden your        │
│  environment.                                             │
│                                                           │
│  ⬜  Back up settings.json                                │
│  ⬜  Encrypt settings.json with Windows DPAPI             │
│  ⬜  Apply new security profile                           │
│                                                           │
│  [Cancel]                          [Upgrade  ▶]          │
└───────────────────────────────────────────────────────────┘
```

Step icons update live as each step runs:

| Icon | Meaning |
|------|---------|
| ⬜ | Pending — not yet started |
| ⏳ | Running |
| ✅ | Completed successfully |
| ⚠️  | Completed with a warning — review the message |
| ❌ | Failed — rollback in progress |

### Cancelling safely

| When you cancel | What happens |
|----------------|--------------|
| Before clicking Upgrade/Downgrade | No change made, dialog closes |
| After a step fails | Completed steps rolled back automatically; backups restored |
| After clicking Cancel when wizard is idle | Profile radio in Settings reverts to previous value |

### Backups created

| Transition | Backup file |
|-----------|-------------|
| Low → Mid (encrypt) | `settings.json.bak` (readable plain JSON) |
| Mid → Low (decrypt) | `settings.json.enc.bak` (encrypted binary) |

Backups are stored in `%AppData%\MyCrownJewelApp\TextEditor\` alongside `settings.json`.

---

## Status-Bar Blocked-Link Feedback

When a link is blocked by the active profile, the status bar shows a specific message
rather than silently doing nothing:

| Profile | Example blocked link | Status bar message |
|---------|---------------------|-------------------|
| Low+ | `javascript:alert(1)` | *🔒 Link blocked (dangerous URI scheme). Change profile in Settings → Security.* |
| Max | `http://prometheus:9090` | *🔒 Link blocked (http:// links are blocked at Max — use https://). Change profile in Settings → Security.* |
| Max | `file:///C:/report.html` | *🔒 Link blocked (file:// and ftp:// links are blocked at Max profile). Change profile in Settings → Security.* |

---

## Build-Time Status Indicators

The **Settings → Security** panel shows read-only build-time indicators that reflect the
state of the binary you are running. These are informational only — they cannot be changed
at runtime.

| Indicator | Meaning |
|-----------|---------|
| ✅ Code signing | The running EXE has an Authenticode digital signature |
| ❌ Code signing | The EXE is unsigned — unsigned binaries from unknown sources could be substituted without detection |
| ✅ CI security gates | The deployment pipeline has Wiz or CodeQL scanning enabled |
| ❌ CI security gates | Security scanning in CI is disabled or commented out |
| ✅ Installer signing | The Inno Setup installer has a `SignTool` directive |
| ❌ Installer signing | The installer is unsigned |

A ❌ on any build-time indicator does not affect runtime enforcement — it is a prompt for
future hardening, not an alarm.

---

## Frequently Asked Questions

**Q: I upgraded to Mid and now settings.json is unreadable. Is my config lost?**

No. Your settings are intact inside the encrypted file. Use **About → Open settings.json**
to export a readable copy to your Desktop for inspection. Alternatively, downgrade to Low
via Settings → Security — the wizard will decrypt the file back to plain JSON automatically.

---

**Q: I moved settings.json to a new machine and pfpad can't load it.**

DPAPI encryption is tied to your Windows user account on the originating machine. Moving the
encrypted file to a new machine will cause decryption to fail. pfpad falls back to defaults
and the settings directory will contain `settings.json.enc.bak` for reference. Re-configure
from defaults on the new machine.

---

**Q: A link I know is safe is being blocked at Max.**

The status bar will show exactly why. If the link uses `http://`, switch to `https://` at
the source, or temporarily switch to Mid profile while you access it. The transition takes
under 2 seconds in each direction.

---

**Q: Can I jump directly from Not Hardened to Max?**

Yes. The transition wizard accumulates all steps needed for the full jump — backup, encrypt
settings, warn about `http://` links — and runs them in sequence.

---

**Q: The build-time indicators all show ❌. Is this dangerous?**

For a personal development machine, unsigned binaries and no CI security gates are common
and expected. The indicators exist to guide future hardening investment, not to create alarm.
The runtime profile (Low/Mid/Max) is what protects you day-to-day.

---

*Security hardening in pfpad is designed to be progressive — start at Low and advance as
your environment and requirements evolve. Every step is guided, every migration is reversible.*
