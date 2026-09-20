# DialShift — project memory

A desktop radio app that switches stations on a weekly schedule. Two independent
UIs share one core: `DialShift` (Windows, WPF + LibVLC) and `DialShift.Desktop`
(Mac, Avalonia + native AVPlayer; also runs as a Windows dev preview). See
`README.md` for user-facing docs and `MACOS.md` for Mac build details.

## Layout

- `DialShift.Core` — models, `Settings`/`SettingsStore` (atomic JSON persistence),
  weekly `Scheduler`/`ScheduleSession`. Shared by both UIs and both test projects.
- `DialShift` — Windows app. `RadioController` wraps `LibVLCSharp.Shared.MediaPlayer`
  for playback (retry/fallback/schedule logic lives here). `MainWindow` builds the
  whole UI in code (no XAML views beyond `App.xaml`). `Visualization/` is the
  player-card visualizer — see below.
- `DialShift.Desktop` — Mac app, separate `RadioController`/`Audio.cs` using
  AVFoundation via `MacAudioSession`. Not touched by the visualizer work.
- `DialShift.Tests` / `DialShift.Desktop.Tests` — run via `dotnet run --project
  <name> -c Release`, plain console assertions (`PASS:`/`FAIL:` output), no test
  framework.

## Environment

- Requires **.NET 10 SDK**. If `dotnet --list-sdks` shows nothing, install with
  `winget install --id Microsoft.DotNet.SDK.10`.
- Windows build: `scripts/build.ps1` (`-SkipTests`, `-OutputDirectory <path>`).
  It always tries to zip to `artifacts/DialShift-<version>-win-x64.zip`, which
  fails if you redirect `-OutputDirectory` elsewhere and `artifacts/` doesn't
  exist — that's a pre-existing script quirk, not a build failure; the publish
  step (the part that matters) still succeeds.
- Smoke test: `DialShift.exe --smoke-test --output <dir>` plays all saved
  stations live for a few seconds each (real network streams), writes
  `results.json` + PNG screenshots of each page. Great for verifying UI/playback
  changes without manual clicking — `Read` the PNGs to see the rendered layout.

## The visualizer (added 2026-09)

Player card (`MainWindow.cs`, inside `playerCard`) has a `VisualizerControl`
strip below the play/skip/volume row, with a "Change graph" button that cycles
`Bars → Scope → Off` (click the strip itself does the same). The chosen style
persists in `Settings.VisualizerStyle`. Control height is 108px (bumped from an
original 64px — kept even after later trimming down to two styles because it
reads better).

- **Scope** auto-gains to the recent peak amplitude (fast attack, slow release,
  floor at 0.02, capped at 18x) — raw loopback amplitude at normal volumes is
  often ~0.01–0.02, which read as a nearly flat line before this was added.
- A third style, **Matrix** (falling-katakana digital rain reactive to audio
  energy/beat), was built and worked, but was removed on user feedback ("δεν
  μου αρέσει" — didn't like it). If a third style is wanted again, don't just
  re-add that one uncritically — ask what look they actually want first.

**Critical constraint — do not hook LibVLC's audio callbacks for this.**
`MediaPlayer.SetAudioCallbacks`/`SetAudioFormatCallback` **replace LibVLC's
entire audio output**; a callback that only reads samples for visualization
(and doesn't render them anywhere) silences real playback. This was tried and
reverted during initial development — it looked correct in testing only because
the smoke-test harness forces `Settings.Volume = 0` (see `App.xaml.cs`), which
happened to hide the regression until it was tested with a nonzero volume.

**What's implemented instead:** `SystemAudioTap` (in `Visualization/`) uses
NAudio's **per-process WASAPI loopback** (`WasapiRecorderBuilder
.WithProcessLoopback(pid, ProcessLoopbackMode.IncludeTargetProcessTree)`,
Windows 10 2004+/build 19041+) to read DialShift's own decoded audio without
touching LibVLC's pipeline at all — it's a passive tap, so it structurally
cannot break sound. `App` owns one `AudioSpectrum` (ring buffer of mono float
samples) and one `SystemAudioTap` for the app's lifetime, independent of
station switching; `RadioController` has no visualizer code in it.

Two dead ends worth remembering if this needs revisiting:
- Plain **default-device** loopback (`WasapiLoopbackCapture`, or
  `WasapiRecorderBuilder().WithLoopbackCapture().WithDevice(defaultDevice)`)
  looked reasonable but silently captured nothing on the dev machine — Windows'
  per-app "App volume and device preferences" can redirect a specific process
  to a different output device than the system default, and default-device
  loopback misses that entirely. Per-process loopback sidesteps the whole
  problem by tapping the process instead of a device.
- NAudio 3.1's `WasapiLoopbackCapture` / old `WasapiOut` are obsolete in favor
  of `WasapiRecorderBuilder`/`WasapiPlayerBuilder`; process loopback specifically
  needs `.BuildAsync()`, not `.Build()` (throws `InvalidOperationException`
  otherwise). The new capture delegate signature is `(ReadOnlySpan<byte> buffer,
  AudioClientBufferFlags flags, long devicePosition, long qpcPosition)`, not the
  old `EventHandler<WaveInEventArgs>`.

`AudioSpectrum.HasSignal` requires a full `FftSize` (1024) samples pushed; until
then (or when muted/paused, since the tap reflects real audible output) the
control shows a "WAITING FOR AUDIO…" placeholder rather than stale/fake data.
`Fft.cs` is a minimal radix-2 Cooley-Tukey implementation — fine for ~28 visual
bars, not a general DSP tool.

## Working conventions here

- Keep `RadioController` (Windows) and the Mac `RadioController`/`Audio.cs` free
  of anything not related to core playback/scheduling — they're covered by the
  desktop test suites and any unrelated coupling makes those harder to trust.
- `MainWindow.cs` builds UI entirely in C# with small static helpers (`Text`,
  `Button`, `Card`). Watch for `DockPanel.LastChildFill` (default `true`) — it
  makes the *last-added* child fill remaining space regardless of its own
  `Dock` value; set `LastChildFill = false` when you want multiple docked
  children to just hug their sides (bit us once with a button that stretched
  full-width because it was the last child added, docked Right).
- When adding NuGet packages, check for near-latest deprecations before writing
  against an API — NAudio 3.x moved several core APIs (loopback capture, output)
  to new builder-based types; the compiler warning (`CS0618`) is reliable but
  worth resolving properly rather than suppressing, since the modern APIs are a
  meaningfully different shape (async build, `Span`-based callbacks).
