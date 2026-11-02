# Verification and limitations

The first public build was tested on Windows 11 x64 with .NET Framework 4.8.1 and the official Codex desktop app. Its 54 dependency-free tests cover quota normalization, reset/freshness behavior, account/workspace identity fencing, bounded JSON-RPC, local settings, startup ownership, image clicks, click-only details toggling, dark-mode error readability and retained mouse-focus outlines.

Native hit testing confirmed the card above Codex at 125% DPI. A temporary foreign-app test confirmed it hides and clears its topmost status, then returns. Light/dark WPF card/details/settings renderings were inspected using synthetic data.

Pure placement tests cover negative monitor origins and 125/150/200% DPI calculations. Mixed-monitor hardware, Windows high contrast, ARM and older Windows were not physically tested. Live automated dropdown/mouse/keyboard traversal was unavailable in the original test environment; semantic WPF tests and static template review were used. Actual resource use varies; no cross-machine CPU/RAM benchmark is claimed.

Window layout/zoom changes may need recalibration. The selected desktop account is not independently verified; inspect the displayed local usage account. An update received while a quota read is in flight may wait for the next normal poll. Future Codex protocol or package identity changes may require maintenance.

## Commands

```powershell
powershell -NoProfile -ExecutionPolicy RemoteSigned -File .\run-tests.ps1
powershell -NoProfile -ExecutionPolicy RemoteSigned -File .\build.ps1
```

The tests use synthetic accounts and project-local fixtures. They do not sign in, log out, make model requests or alter real Windows startup entries. Native probes are opt-in commands, separate from the normal test suite.

