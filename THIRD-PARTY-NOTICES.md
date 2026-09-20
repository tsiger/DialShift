# Third-party components

DialShift uses unmodified, dynamically linked dependencies:

## Mac release

The Mac build uses macOS AVFoundation/AVPlayer (provided by the operating system)
and does not bundle VLC. It bundles:

- **Avalonia 12.1.2** — MIT. [Source](https://github.com/AvaloniaUI/Avalonia/tree/12.1.2).
- **SkiaSharp 3.119.4 / HarfBuzzSharp 8.3.1.3** — MIT managed bindings plus native
  Skia/HarfBuzz and component licenses. [Source](https://github.com/mono/SkiaSharp).
- **MicroCom.Runtime 0.11.6** — MIT. [Source](https://github.com/kekekeks/MicroCom).
- **Tmds.DBus.Protocol 0.94.1** — MIT (transitive Avalonia Linux support; unused on Mac).
  [Source](https://github.com/tmds/Tmds.DBus).
- **.NET 10** — MIT and component notices. [Source](https://github.com/dotnet/runtime).

Full license texts and native component notices are included in `licenses/` in the
repository and the app bundle's `Contents/Resources`. The self-contained runtime
also has Microsoft's license and notice files in `Contents/Resources/licenses`.

The build-only [apple-codesign/rcodesign](https://github.com/indygreg/apple-platform-rs)
tool creates ad-hoc preview signatures; it is not bundled with the app.

## Windows release

- **LibVLCSharp 3.10.1** — LGPL-2.1-or-later. [Project and source](https://code.videolan.org/videolan/LibVLCSharp), [NuGet](https://www.nuget.org/packages/LibVLCSharp/3.10.1).
- **VideoLAN.LibVLC.Windows 3.0.23.1** — VLC/LibVLC native runtime and plugins. LibVLC is LGPL-2.1-or-later; bundled plugins and their dependencies have individual licenses, including GPL components. [Packaging/source instructions](https://code.videolan.org/videolan/libvlc-nuget), [VLC source](https://code.videolan.org/videolan/vlc/-/tree/3.0.23), [VideoLAN legal information](https://www.videolan.org/legal.html).
- **NAudio 3.1.0** — MIT. Used only to read DialShift's own decoded audio (WASAPI process loopback) to drive the player card's visualizer; it does not touch playback. [Source](https://github.com/naudio/NAudio), [NuGet](https://www.nuget.org/packages/NAudio/3.1.0).
- **.NET 10 / WPF / Windows Forms** — Microsoft and contributors, MIT and component notices. [Runtime](https://github.com/dotnet/runtime), [WPF](https://github.com/dotnet/wpf), [Windows Forms](https://github.com/dotnet/winforms). The self-contained release carries runtime license/notice files.

Native VLC libraries remain separate in `libvlc/win-x64` so compatible replacements can be supplied. No changes to these third-party libraries were made. Station names, broadcasts and programming belong to the respective providers. The included links are for personal listening.
