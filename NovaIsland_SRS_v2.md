# Nova Island — SRS & Technical Architecture v2.0

## 0. Changelog vs v1.0

| Area | v1.0 | v2.0 |
|---|---|---|
| Rendering | Composition API (general) | Composition-only hot path, XAML banned from island morph, DirectComposition visual tree, DXGI refresh-sync frame pacing |
| Stability | Not addressed | Watchdog process, per-module crash isolation, self-healing restarts |
| Plugins | In-process SDK | Sandboxed (separate process/WASM), capability manifest |
| Performance | Static targets | Native AOT, working-set trimming, hot-path zero-alloc rule, hardware tier fallback |
| Windows integration | Generic | DWM extended frames, per-monitor DPI, taskbar coexistence, enterprise policy, Sandbox test matrix |
| Accessibility | Mentioned | Reduced-motion mode, screen reader (UIA) contract per module |
| Features | 14 core | +22 (see §5) |
| Roadmap | 7 phases | 10 phases (added Stability Core, Performance Hardening, Scaffolding) |

---

## 1. Vision

Nova Island is a premium, native Windows 11 shell companion (Dynamic Island–style). Primary differentiators: **rock-solid stability**, **GPU-native 120 FPS animation**, and deep, non-intrusive Windows integration — not feature count.

## 2. Objectives

| Objective | Target |
|---|---|
| Native feel | Zero XAML jank on hot path, DWM-correct rounded/blur window |
| Idle RAM | < 40 MB (tightened from 50 MB) |
| Idle CPU | < 0.2% |
| Animation | 120 FPS, adaptive to display refresh (60/120/144/165 Hz) |
| Startup | < 300 ms cold, < 100 ms warm (post Native AOT) |
| Stability | Zero full-app crashes from plugin/module faults (isolated failure domains) |
| Extensibility | Plugin load < 100 ms, sandboxed |
| Enterprise readiness | MDM/Group Policy support, signed packages |

## 3. Technology Stack

| Layer | Choice | Notes |
|---|---|---|
| Language | C# 13 / .NET 9 | Native AOT publish profile |
| UI shell | Win32 layered window + Windows.UI.Composition | XAML only for secondary panels (settings, marketplace), never the island itself |
| Widgets/panels | WinUI 3 + Windows App SDK | Isolated from hot-path rendering |
| GPU interop | Vortice.Windows (DXGI/Direct2D) | Frame pacing, refresh-rate detection |
| Animation | Custom spring-physics engine over Composition | Interruptible, gesture-driven |
| Architecture | Clean Architecture + MVVM (CommunityToolkit.Mvvm) | |
| DI/Hosting | Microsoft.Extensions.Hosting | Generic Host, background services |
| Data | SQLite + EF Core (encrypted via SQLCipher for clipboard/PII) | |
| Logging | Serilog (rolling file + ETW sink) | |
| Packaging | MSIX (packaged) + sparse MSIX (unpackaged fallback) | |
| Updater | Velopack, staged channels (canary/stable) | |
| Watchdog | Separate minimal Win32 console process | Named-pipe IPC, no WinRT deps |
| Plugin sandbox | Child process isolation (default) or WASM (Wasmtime) for untrusted plugins | Capability manifest |
| Testing | xUnit, FluentAssertions, WinAppDriver/Playwright, dotnet-trace for perf gates | |
| CI/CD | GitHub Actions, hardware-matrix perf gate | |

## 4. High-Level Architecture

```
Presentation      → Island Shell (Win32+Composition) | Panels (WinUI3)
Application        → Use cases, MVVM ViewModels, module orchestrators
Domain             → Entities, rules, automation DSL
Infrastructure     → SQLite, Serilog, OS interop (WinRT/Win32), providers
Watchdog (external)→ Independent process, IPC health-check
Plugin Host (ext.) → Sandboxed process/WASM runtime
```

Modules: Island Core, Media, Clipboard, Notifications, Widgets, Quick Launcher, Calendar, Automation, Phone Link, Gaming Mode, AI Assistant, Plugin SDK, Marketplace, Stability (watchdog client), Telemetry.

## 5. Feature Catalog

### 5.1 Core (from v1.0)
Dynamic Island shell, Media Controls, Clipboard History, Screenshot+OCR, Notification Center, Widgets, Quick Launcher, Calendar, Automation, Phone Link, Gaming Mode, AI Assistant, Live Activities, Plugin Marketplace.

### 5.2 New — Visual/Animation
| Feature | Detail |
|---|---|
| Adaptive shape morphing | Compact/Expanded/Minimal/Alert states via spring physics, interruptible mid-animation |
| Frame-pacing engine | DXGI refresh-rate detection, VSync-aligned scheduler, no dropped frames under CPU load |
| Reduced-motion mode | Accessibility toggle; swaps springs for instant/cross-fade transitions |
| Ambient light auto-theming | Sensor API drives Mica/Acrylic tint adaptation |
| GPU tier fallback | Detects integrated vs discrete GPU; degrades blur/shadow quality gracefully |

### 5.3 New — Stability
| Feature | Detail |
|---|---|
| Watchdog process | Independent process restarts main app on hang/crash (<2 s) |
| Module crash isolation | Each module has its own exception boundary; failure restarts only that module |
| Plugin sandboxing | Plugins run out-of-process or in WASM; crash cannot touch host |
| Self-healing updates | Velopack auto-rollback after N consecutive crash-loop detections |
| Memory/handle leak guard | Periodic working-set + handle count diagnostics, auto-trim |

### 5.4 New — Windows Integration
| Feature | Detail |
|---|---|
| Multi-monitor awareness | Per-monitor DPI, island follows active/cursor monitor, configurable |
| DWM extended frame / Mica/Acrylic | Native blur via DWM APIs, not custom compositing |
| Taskbar coexistence | Guaranteed no z-order/input conflicts with Explorer taskbar |
| Snap Layouts quick access | Native Windows 11 snap integration |
| Virtual Desktop switcher | Live thumbnails via DWM thumbnail API |
| Focus Assist sync | Notification suppression respects OS Focus Assist state |
| Windows Hello gating | Secure quick actions (e.g., reveal clipboard secrets) |
| Group Policy / MDM | Enterprise config via ADMX + Intune CSP |
| Windows Sandbox compatibility | Verified clean install/run in Windows Sandbox |

### 5.5 New — Productivity/Other
Nearby Share integration, Battery/Power HUD, Smart Volume/Brightness HUD, local-only performance telemetry dashboard, voice commands, cross-device sync (stretch), theme marketplace (stretch).

## 6. Animation & Rendering Architecture

- Island shell renders exclusively via `Windows.UI.Composition` visual tree owned by a raw Win32 layered window (`WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`) — **not** a XAML `Window`, to avoid XAML layout/measure passes on every frame.
- State machine: `Compact → Expanded → Minimal → Alert`, each transition driven by a critically-damped spring (configurable stiffness/damping), interruptible at any point (re-target, don't restart).
- Frame pacing: `DispatcherQueueTimer` synced to `DXGI_OUTPUT_DESC` refresh rate; animation updates use delta-time, not fixed-tick, to stay correct across 60/120/144/165 Hz.
- Secondary panels (settings, marketplace, widget gallery) use WinUI 3 XAML — acceptable jank budget since they're not always-visible.
- Hot path zero-alloc rule: no LINQ, no boxing, no per-frame allocations in the animation/render loop (validated via `dotnet-trace` GC gate in CI).

## 7. Stability Architecture

- Watchdog: separate lightweight process, named-pipe heartbeat every 500 ms; missed 4 consecutive heartbeats → restart main process; logs to Serilog with crash reason.
- Each module (Media, Clipboard, Notifications, …) runs inside its own `try/catch` supervision boundary at the hosted-service level; unhandled exception → Serilog capture + exponential-backoff module restart, island shell stays alive.
- Plugins: default isolation is a child process communicating via a thin IPC contract; only signed, reviewed plugins may opt into in-process WASM execution for lower latency.
- Crash-loop detection (N crashes in T minutes) triggers Velopack rollback to last-known-good version automatically.

## 8. Performance Architecture

- Native AOT publish profile for `NovaIsland.App` (trimmed, ReadyToRun fallback if AOT-incompatible dependency found).
- Lazy module initialization: shell + core boot in < 300 ms; Media/Clipboard/Notifications/Widgets/AI initialize on first use or after shell-ready idle callback.
- Idle working-set trimming via `SetProcessWorkingSetSize` on a debounce timer after N seconds of no interaction.
- All OS interop (WinRT projections) wrapped to avoid marshaling in loops; batch calls where possible.

## 9. Windows Integration Requirements

- DWM: use `DwmExtendFrameIntoClientArea` + `DwmSetWindowAttribute` (Mica/Acrylic, rounded corners) instead of hand-rolled blur.
- Per-monitor-v2 DPI awareness manifest; island repositions correctly on monitor hot-plug/DPI change.
- No global low-level hooks that risk Explorer/taskbar hang timeouts; use modern APIs (`UserNotificationListener`, `GlobalSystemMediaTransportControlsSessionManager`) over `SetWindowsHookEx` wherever possible.
- Group Policy/Intune: ADMX template for enterprise-managed feature toggles (disable AI, disable plugin sideloading, force update channel).

## 10. Non-Functional Requirements / Performance Targets

| Metric | Target | Verification |
|---|---|---|
| RAM idle | < 40 MB | dotnet-counters, 10-min idle soak |
| CPU idle | < 0.2% | PerfView, 10-min idle soak |
| Cold startup | < 300 ms | stopwatch from process start to shell-visible |
| Warm startup | < 100 ms | AOT build, second launch |
| Animation | 120 FPS sustained, adaptive to display Hz | dotnet-trace frame-time histogram, p99 < 8.3 ms @120Hz |
| Plugin load | < 100 ms | per-plugin load timer |
| Crash isolation | 0 full-app crashes from module/plugin fault | fault-injection test suite |
| Watchdog recovery | < 2 s | kill -9 test harness |

## 11. Engineering Standards

SOLID, async/await everywhere (never block UI thread), event-driven services, comprehensive Serilog logging with correlation IDs, XML doc comments on public APIs, UIA accessibility contract per visible module, high-DPI aware, unit + integration + perf-gate tests in CI, nullable reference types enabled, warnings-as-errors.

## 12. Folder Structure

```
src/
  NovaIsland.App              # host, DI composition root, Native AOT entry
  NovaIsland.UI                # Win32+Composition island shell (hot path)
  NovaIsland.Panels            # WinUI3 secondary panels (settings, marketplace)
  NovaIsland.Application        # use cases, orchestrators
  NovaIsland.Domain             # entities, automation DSL, rules
  NovaIsland.Infrastructure     # SQLite/EF Core, Serilog, OS interop, providers
  NovaIsland.Watchdog           # independent process
  NovaIsland.Plugins.Host       # sandbox runtime (process/WASM)
  NovaIsland.SDK                # public plugin API surface
tests/
  NovaIsland.Tests.Unit
  NovaIsland.Tests.Integration
  NovaIsland.Tests.Perf         # dotnet-trace based perf gates
docs/
installer/                    # MSIX, Velopack channel configs
```

## 13. Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| XAML jank on island shell | Fails 120 FPS target | Ban XAML from hot path (§6), enforced by architecture test |
| Plugin crashes host | Violates stability objective | Out-of-process/WASM sandbox (§7) |
| Global hooks destabilize Explorer | System-wide hang | Prefer modern listener APIs over SetWindowsHookEx |
| Native AOT incompatible dependency | Startup target missed | ReadyToRun fallback, dependency audit in Phase 0 |
| Multi-monitor/DPI edge cases | Visual glitches | Dedicated test matrix (§9), per-monitor-v2 manifest |
| Store certification rejection | Delayed release | Early Store policy review during Phase 9 planning |

## 14. Development Roadmap

| Phase | Name | Focus |
|---|---|---|
| 0 | Scaffolding | Solution structure, CI, DI/logging bootstrap, Native AOT config |
| 1 | Foundation + Island Shell | Composition shell, spring animation, multi-monitor, frame pacing |
| 2 | Stability Core | Watchdog, module crash isolation, self-healing |
| 3 | Media & Clipboard | SMTC integration, encrypted clipboard history |
| 4 | Notifications & Widgets | Notification listener, widget framework, Focus Assist sync |
| 5 | AI + Automation | Pluggable AI provider, async rule engine |
| 6 | Plugin SDK | Sandboxed plugin runtime, capability manifest |
| 7 | Marketplace | Signed package distribution, install/update flow |
| 8 | Performance & Animation Hardening | Profiling pass, hardware-matrix validation against §10 |
| 9 | Store Release | MSIX/Velopack staged rollout, enterprise deployment, certification |

## 15. Master AI Prompt

> Act as a senior Windows platform architect. Build Nova Island using C# / .NET 9, a Win32+Composition island shell (no XAML on the hot path), WinUI 3 for secondary panels, Clean Architecture + MVVM, Microsoft.Extensions.Hosting for DI, SQLite+EF Core, Serilog, Velopack, and a separate watchdog process for crash recovery. Enforce: zero-alloc animation loop, DXGI refresh-synced frame pacing, per-module crash isolation, sandboxed plugins, Native AOT startup, and the performance targets in §10 of the SRS. Prioritize stability and 120 FPS visual smoothness over feature breadth at every phase. Generate production-ready, modular, tested, documented code — no shortcuts, no placeholder TODOs in delivered code.