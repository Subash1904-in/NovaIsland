# Native AOT Compatibility Report — Nova Island Phase 0

> Generated during Phase 0 scaffolding. This report covers all NuGet packages
> referenced in the Phase 0 scaffold and known packages from the SRS technology stack.

## Summary

| Package | Version | AOT Compatible | Notes |
|---|---|---|---|
| Microsoft.Extensions.Hosting | 9.0.0 | ✅ Yes | Full AOT support since .NET 8. Source-generated DI available. |
| Serilog | 4.2.0 | ✅ Yes | No reflection-heavy code paths. Message templates are compile-time. |
| Serilog.Extensions.Hosting | 8.0.0 | ✅ Yes | Integrates via standard `IServiceCollection` — no dynamic proxy generation. |
| Serilog.Sinks.Console | 6.0.0 | ✅ Yes | Pure output formatting, no reflection. |
| Serilog.Sinks.File | 6.0.0 | ✅ Yes | File I/O only, no reflection. |
| Serilog.Enrichers.Thread | 4.0.0 | ✅ Yes | Reads `Thread.CurrentThread` — trivially AOT-safe. |
| Serilog.Enrichers.Environment | 3.0.1 | ✅ Yes | Reads `Environment.*` properties — trivially AOT-safe. |
| xunit | 2.9.3 | N/A | Test-only; not included in published output. |
| FluentAssertions | 7.1.0 | N/A | Test-only; not included in published output. |
| coverlet.collector | 6.0.3 | N/A | Test-only; not included in published output. |

## Future Packages (from SRS §3 — not yet referenced)

| Package | AOT Compatible | Risk | Mitigation |
|---|---|---|---|
| CommunityToolkit.Mvvm | ✅ Yes | None | Uses source generators, no reflection. Fully AOT-safe. |
| Vortice.Windows (DXGI/Direct2D) | ✅ Yes | None | P/Invoke-based COM interop. No reflection or dynamic code generation. |
| Microsoft.EntityFrameworkCore.Sqlite | ⚠️ Partial | Medium | EF Core relies on reflection for model building and query compilation. **Mitigations**: Use precompiled queries (`EF.CompileAsyncQuery`), `IDbContextFactory`, and compiled models (`dotnet ef dbcontext optimize`). SQLite provider itself is AOT-safe. |
| SQLitePCLRaw | ✅ Yes | None | Native SQLite bindings via P/Invoke. |
| Velopack | ✅ Yes | Low | Thin native wrapper for update checks. No dynamic code generation. |
| Windows App SDK (WinUI 3) | ⚠️ Partial | Medium | XAML compilation uses reflection for `x:Bind` fallbacks. **Mitigation**: XAML is only used in Panels (secondary, non-AOT-critical path). The island shell (hot path) uses Win32+Composition only, which is fully AOT-compatible. Consider excluding Panels from AOT publish and loading them dynamically. |
| Wasmtime-dotnet | ✅ Yes | Low | Native WASM runtime with P/Invoke bindings. |

## Recommendations

1. **Phase 0 (current)**: All referenced packages are fully AOT-compatible. `dotnet publish` with `PublishAot=true` should succeed cleanly.

2. **Phase 3+ (EF Core)**: When adding Entity Framework Core:
   - Use compiled models (`dotnet ef dbcontext optimize`) to eliminate runtime model building reflection.
   - Use `EF.CompileAsyncQuery` for all query patterns.
   - Test AOT publish after every EF Core addition.

3. **Phase 1 (WinUI 3 Panels)**: Consider a hybrid publish strategy:
   - AOT-compile `NovaIsland.App` and the hot-path shell (`NovaIsland.UI`).
   - Use ReadyToRun for `NovaIsland.Panels` if WinUI 3 XAML introduces AOT warnings.
   - The SRS already architecturally isolates XAML from the hot path, so this has no performance impact.

4. **Fallback Strategy** (per SRS §13, Risk Register): If any dependency blocks AOT in a future phase, switch that specific project to ReadyToRun compilation. The entry point (`NovaIsland.App`) and shell (`NovaIsland.UI`) must remain AOT-compiled to meet the <300ms cold startup target.

## ETW Sink Note

The SRS §3 mentions `Serilog (rolling file + ETW sink)`. The ETW sink (`Serilog.Sinks.ETW`) uses `EventSource` which is fully AOT-compatible on .NET 9. It will be added in a later phase when telemetry infrastructure is built out.
