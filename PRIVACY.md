# Privacy and local data

## What the widget reads

The widget invokes the official Codex CLI already installed for the current Windows user. Its private stdio connection permits initialization, `account/read` and `account/rateLimits/read`. It does not invoke model/inference, chat-history, login or logout methods. The official process manages its own existing sign-in and network requests; the widget does not read credential files directly.

Account email, workspace label, stable routing identifiers and quota snapshots are held in memory. The email/workspace appear locally in usage details so you can check the source. They are not written into widget settings, uploaded to this repository or included in the portable download. Account/workspace identity is checked before and after each accepted quota read. The selected desktop UI account cannot be independently certified.

Native Windows APIs inspect window/process/package identity, window bounds, visibility and foreground state to attach the card. There is no Codex debugger connection, webpage injection or network listener.

## Files on your computer

- `data/settings.json`: your appearance, image choice and position. No usage or account identifiers.
- `data/images/`: copies of custom images you explicitly choose. These may contain personal material; keep this folder private.
- `data/startup-owner.txt`: a random ownership marker created if you enable startup.
- `out/status.json`: local lifecycle/placement status, with no quota percentages or account labels. Removed on orderly exit.
- Optional diagnostic commands print remaining/reset status or native window metadata. Review output before sharing it.

The official Codex process may maintain its normal local state independently. The widget adds no analytics or telemetry endpoint. Its source is available for inspection; this statement does not replace the official Codex app's privacy terms.

## What is distributed

The public repository contains source, synthetic tests, documentation, scripts, default generated art and a portable release. Each release is assembled from an explicit list of runtime files. It contains **no saved settings, imported personal images, account cache, credentials, diagnostic dumps or development-session records**. Startup is disabled until the recipient explicitly enables it.

The maintainer's local paths and personal email are excluded. Public GitHub account/repository information and a GitHub no-reply commit address remain visible as normal repository metadata. A publication audit checks the chosen files and compiled archive; it cannot guarantee the absence of every possible future defect or prevent someone from deliberately publishing their own private files.

## Safe contributions and bug reports

Do not commit `data`, build/test output, `.env`, auth files, tokens or private development notes. The repository's `.gitignore` excludes these common paths. Never attach a screenshot containing your account email without redacting it. Use synthetic fixtures such as `sample@example.test` in tests.

You can quit from the card/notification-area menu. Uninstall removes only the proven-owned startup shortcut and offers removal of saved settings and imported image copies. Your original source image and Codex installation remain untouched.

