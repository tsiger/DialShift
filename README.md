# DialShift

**Your radio, on time.** A desktop radio for Windows and Mac that automatically
switches stations to follow your weekly listening schedule.

## Download

| Platform | Ready-to-run app | Requirements |
| --- | --- | --- |
| Windows | **[Download Windows ZIP](https://github.com/tsiger/DialShift/releases/download/v0.2.0/DialShift-0.2.0-win-x64.zip)** | Windows 10/11, x64 |
| Mac | **[Download Mac ZIP](https://github.com/tsiger/DialShift/releases/download/v0.2.0/DialShift-0.2.0-osx-arm64.zip)** | Apple Silicon (M-series), macOS 14 Sonoma or newer |

**[Release notes and checksums](https://github.com/tsiger/DialShift/releases/tag/v0.2.0)** · [All releases](https://github.com/tsiger/DialShift/releases)

Both downloads include .NET. Windows also bundles VLC; Mac uses the built-in macOS
audio player. **No separate runtime or player installation is needed.** Intel Macs,
Windows ARM and Linux do not have release downloads yet.

GitHub's **Code → Download ZIP** and the **Source code** archives contain source
files. Use the platform links above to get the ready-to-run app.

### Windows

1. Download and extract the entire Windows ZIP.
2. Open **DialShift.exe** inside the extracted folder.
3. Keep the whole folder together, including its libraries.

The app is portable and does not require administrator rights. Optional install:
right-click `Install.ps1` in the extracted folder and choose **Run with PowerShell**.
This copies the app to `%LOCALAPPDATA%\Programs\DialShift` and adds a Start menu
shortcut. Launch at sign-in is an opt-in setting.

### Mac

1. Download and unzip the Mac ZIP.
2. Drag **DialShift.app** into **Applications**, then open it. Running from another
   folder, such as the Desktop, also works.
3. The app is ad-hoc signed, **not Apple-notarized**. If macOS blocks it, attempt to
   open it once, then use **System Settings → Privacy & Security → Open Anyway**
   for DialShift and confirm Open.

To update either platform, quit DialShift using its tray/menu-bar menu before
replacing the app files. Your stations and schedule are stored separately and remain
intact. [More Mac setup, import and build instructions](MACOS.md).

## Make radio a routine

- **Stations → Add station:** enter a name, optional description and direct HTTP/HTTPS
  audio stream URL. MP3, AAC and HLS are supported. Use a stream URL, not the station's
  webpage or a playlist that requires choosing a child stream.
  *(Windows: the Stations tab shows your stations as tiles — hover one and click **Play**
  to tune in. Adding and editing stations lives in the **Edit stations** tab.)*
- **Schedule → Add time slot:** choose a station, 24-hour start time and days. Add an
  optional show label. Conflicting enabled slots on the same day/time are rejected.
- Turn on **Follow my schedule** to tune into the latest matching slot immediately.
  Each station plays until the next scheduled start; there are no end-time/stop slots.
- Pick a station manually or press **Pause** to override the current slot until the
  next scheduled switch. Play reconnects to the live broadcast.
- **Windows:** the player card shows a live visualizer under the play/skip controls.
  Click it (or **Change graph**) to cycle Bars, Scope and Off, Winamp-style;
  your choice is remembered. It reads DialShift's own decoded audio via Windows'
  per-process WASAPI loopback (Windows 10 2004+/Windows 11) and never touches
  playback, so muting or a very old Windows build just leaves it idle.
- Schedules repeat weekly in your computer's local time zone and catch up after
  sleep or a missed start. DialShift does not wake a sleeping computer. A repeated
  daylight-saving occurrence fires once per running session.
- Failed streams retry, then use your optional fallback after three failures. While
  a fallback plays, DialShift retries the original every two minutes.
- Closing the window keeps the app running in the Windows tray or Mac menu bar.
  Reopen it from the icon; use the icon's menu for playback, stations, volume,
  schedule toggle and **Quit DialShift**. The Mac Dock icon also reopens the window.
- **Settings:** optional launch at login, start hidden, fallback station and settings
  folder. Playback stays idle on first launch until you press Play or enable a schedule.

Both builds start with three SomaFM stations:
[Groove Salad](https://somafm.com/groovesalad/directstreamlinks.html),
[Drone Zone](https://somafm.com/dronezone/directstreamlinks.html), and
[Secret Agent](https://somafm.com/secretagent/directstreamlinks.html).
Add your own favorites. Personal stations and schedules are never bundled in releases.

### Mac status

Playback and station/schedule editing have been manually confirmed on an Apple
Silicon Mac running macOS 15.7.3. This release includes the fix for a crash when
refreshing the menu after edits. The Mac interface has some cosmetic rough edges;
track-title metadata is not available yet, so it shows the station description.
Actual Mac login and hardware sleep/wake behavior still need broader testing.

## Your data

| Platform | Settings and logs |
| --- | --- |
| Windows | `%LOCALAPPDATA%\DialShift\` |
| Mac | `~/Library/Application Support/DialShift/` |

Preferences are saved atomically in `settings.json`. Unreadable files are preserved
as `settings.json.unreadable-*` before defaults are used. Errors go to `dialshift.log`.
No account, analytics, server or cloud sync; playback connects directly to the selected
radio provider.

**Move your Windows stations to Mac:** copy the Windows `settings.json`, then choose
**Settings → Import stations & schedule…** in the Mac app (scroll down). Import asks
before replacing the current stations/schedule and saves a backup. Mac also offers
**Export stations & schedule…**. Startup settings stay specific to each installation.

## Build and verify

Requires a .NET 10 SDK. The PowerShell scripts also recognize the local SDK at
`%LOCALAPPDATA%\DialShift\sdk`.

```powershell
# Windows app, built on Windows
./scripts/build.ps1

# Apple Silicon app, cross-built on Windows; also requires Python 3
./scripts/build-macos.ps1

# Deterministic scheduling, persistence and playback-controller checks
dotnet run --project DialShift.Tests -c Release
dotnet run --project DialShift.Desktop.Tests -c Release
```

Builds go to ignored `artifacts/` directories. To publish Windows into a separate
folder while an existing copy is running, pass `-OutputDirectory C:\path\to\output`.
The Mac script bundles the app, ad-hoc signs it, verifies code/resource hashes and
creates a ZIP with Unix executable permissions. See [MACOS.md](MACOS.md) for details.

The Windows and Mac executables support `--smoke-test --output <folder>` for isolated,
muted playback and UI checks. They write `results.json` and screenshots, then exit.
Live-stream checks require network access. Windows additionally supports
`--recovery-test` with `--smoke-test` to exercise retry/fallback against an unavailable
local endpoint. Mac checks cover editor validation, save/edit/delete, persistence,
and menu refreshes. Hardware sleep, login and audible output require target-machine
testing.

## Project

- `DialShift.Core`: shared models, persistence, weekly scheduling and occurrence tracking.
- `DialShift`: Windows WPF interface, tray controls, LibVLC playback and Windows integration.
  `Visualization/` holds the player-card visualizer (WASAPI loopback capture, FFT, render styles).
- `DialShift.Desktop`: Mac Avalonia interface, menu-bar controls and native AVPlayer audio.
  Its development-only Windows preview uses separate `DialShift-Preview` preferences.
- `DialShift.Tests`: scheduling and persistence checks.
- `DialShift.Desktop.Tests`: playback-controller checks with a simulated audio backend.

Station names and broadcasts belong to their respective providers. DialShift is
unaffiliated. Stream URLs can change; edit a station to update its URL.
See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for bundled dependencies.
