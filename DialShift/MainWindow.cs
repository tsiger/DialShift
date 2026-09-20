using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DialShift.Core;
using DialShift.Visualization;

namespace DialShift;

public sealed class MainWindow : Window
{
    private readonly App app;
    private readonly StackPanel page = new();
    private readonly TextBlock stationTitle = Text("Your next favorite frequency.", 30, true);
    private readonly TextBlock status = Text("READY WHEN YOU ARE", 11, true, "#C2F278");
    private readonly TextBlock track = Text("Choose a station below to start listening.", 14, false, "#A3B4B6");
    private readonly TextBlock upNext = Text("Build a routine in Schedule →", 13, false, "#A3B4B6");
    private readonly TextBlock pageTitle = Text("Your stations", 26, true);
    private readonly TextBlock pageDescription = Text("A few good frequencies. Always within reach.", 13, false, "#A3B4B6");
    private readonly Button play;
    private readonly Slider volume;
    private readonly TextBlock volumeLabel = Text("60%", 12, false, "#A3B4B6");
    private readonly Button[] navigation = new Button[4];
    private readonly VisualizerControl visualizer;
    private readonly TextBlock visualizerLabel = Text("VISUALIZER · BARS", 10, true, "#81989A");
    private int tab;
    private int selectedDay = ((int)DateTime.Now.DayOfWeek + 6) % 7;
    public static readonly DayOfWeek[] Week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    public static Brush Brush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));

    public MainWindow(App app)
    {
        this.app = app;
        Style = (Style)app.FindResource(typeof(Window));
        NativeChrome.Apply(this);
        Title = "DialShift";
        Width = 1050; Height = 860; MinWidth = 780; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/dialshift.ico"));
        var shell = new Grid { Margin = new Thickness(30, 22, 30, 20) };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = shell;

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 25) };
        var trayButton = Button("↘  Hide to tray", () => app.HideToTray());
        DockPanel.SetDock(trayButton, Dock.Right); header.Children.Add(trayButton);
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(Text("◴", 34, true, "#C2F278", new Thickness(0, -6, 10, 0)));
        brand.Children.Add(Text("DialShift", 24, true));
        brand.Children.Add(Text("YOUR RADIO, ON TIME.", 10, true, "#81989A", new Thickness(20, 12, 0, 0)));
        header.Children.Add(brand); shell.Children.Add(header);

        var playerGrid = new Grid();
        playerGrid.ColumnDefinitions.Add(new ColumnDefinition());
        playerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        var information = new StackPanel();
        information.Children.Add(status);
        stationTitle.Margin = new Thickness(0, 12, 12, 6); stationTitle.TextWrapping = TextWrapping.NoWrap; stationTitle.TextTrimming = TextTrimming.CharacterEllipsis; information.Children.Add(stationTitle);
        track.TextTrimming = TextTrimming.CharacterEllipsis; information.Children.Add(track);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 0) };
        play = Button("▶  Play", () => { app.Radio.Toggle(); app.Save(); }, true); play.MinWidth = 112;
        controls.Children.Add(play); controls.Children.Add(Button("Skip  →", () => { app.Radio.NextStation(); app.Save(); }));
        var volumePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        volumePanel.Children.Add(Text("VOL", 10, true, "#A3B4B6", new Thickness(0, 0, 8, 0)));
        volume = new Slider { Minimum = 0, Maximum = 100, Value = app.Settings.Volume, Width = 105, VerticalAlignment = VerticalAlignment.Center, SmallChange = 1, LargeChange = 10, TickFrequency = 1, IsSnapToTickEnabled = true };
        AutomationProperties.SetName(volume, "Playback volume");
        volume.ValueChanged += (_, _) => { app.Radio.SetVolume((int)volume.Value); volumeLabel.Text = $"{(int)volume.Value}%"; };
        volume.LostMouseCapture += (_, _) => app.Save(); volume.KeyUp += (_, _) => app.Save();
        volumePanel.Children.Add(volume); volumeLabel.Margin = new Thickness(8, 0, 0, 0); volumePanel.Children.Add(volumeLabel); controls.Children.Add(volumePanel);
        information.Children.Add(controls); playerGrid.Children.Add(information);
        var dial = new Grid { Width = 154, Height = 154, HorizontalAlignment = HorizontalAlignment.Right };
        dial.Children.Add(new System.Windows.Shapes.Ellipse { Stroke = Brush("#3D5141"), StrokeThickness = 1 });
        dial.Children.Add(new System.Windows.Shapes.Ellipse { Stroke = Brush("#C2F278"), StrokeThickness = 3, Margin = new Thickness(15) });
        var dialMark = Text("◴", 74, false, "#C2F278", new Thickness(0, -8, 0, 0));
        dialMark.HorizontalAlignment = HorizontalAlignment.Center; dialMark.VerticalAlignment = VerticalAlignment.Center;
        dial.Children.Add(dialMark);
        Grid.SetColumn(dial, 1); playerGrid.Children.Add(dial);
        playerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        playerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        playerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(information, 0); Grid.SetRow(dial, 0);
        visualizer = new VisualizerControl(app.Spectrum, ParseStyle(app.Settings.VisualizerStyle));
        var visualizerHeader = new DockPanel { Margin = new Thickness(0, 18, 0, 6), LastChildFill = false };
        visualizerLabel.Text = "VISUALIZER · " + app.Settings.VisualizerStyle.ToUpperInvariant();
        visualizerLabel.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(visualizerLabel, Dock.Left); visualizerHeader.Children.Add(visualizerLabel);
        var visualizerButton = Button("Change graph ⟳", () => visualizer.CycleStyle());
        DockPanel.SetDock(visualizerButton, Dock.Right); visualizerHeader.Children.Add(visualizerButton);
        Grid.SetRow(visualizerHeader, 1); Grid.SetColumnSpan(visualizerHeader, 2); playerGrid.Children.Add(visualizerHeader);
        visualizer.StyleChanged += style =>
        {
            app.Settings.VisualizerStyle = style.ToString();
            visualizerLabel.Text = "VISUALIZER · " + style.ToString().ToUpperInvariant();
            app.Save();
        };
        Grid.SetRow(visualizer, 2); Grid.SetColumnSpan(visualizer, 2); playerGrid.Children.Add(visualizer);
        var playerCard = Card(playerGrid, "#203029", new Thickness(26, 22, 26, 22));
        Grid.SetRow(playerCard, 1); shell.Children.Add(playerCard);

        var section = new StackPanel { Margin = new Thickness(0, 22, 0, 16) };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
        var names = new[] { "Stations", "Edit stations", "Schedule", "Settings" };
        for (var i = 0; i < names.Length; i++) { var index = i; navigation[i] = Button(names[i], () => { tab = index; RefreshPage(); }); tabs.Children.Add(navigation[i]); }
        section.Children.Add(tabs); section.Children.Add(pageTitle); pageDescription.Margin = new Thickness(0, 5, 0, 0); section.Children.Add(pageDescription);
        Grid.SetRow(section, 2); shell.Children.Add(section);
        var scroll = new ScrollViewer { Content = page, Padding = new Thickness(0, 0, 8, 0) }; Grid.SetRow(scroll, 3); shell.Children.Add(scroll);
        var footer = new DockPanel { Margin = new Thickness(0, 18, 0, 0) };
        var local = Text("LOCAL TIME · " + TimeZoneInfo.Local.StandardName, 10, false, "#81989A"); DockPanel.SetDock(local, Dock.Right); footer.Children.Add(local); footer.Children.Add(upNext);
        Grid.SetRow(footer, 4); shell.Children.Add(footer);
        Closing += (_, e) => { e.Cancel = true; app.HideToTray(); };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
        app.Radio.Changed += UpdatePlayer;
        RefreshPage(); UpdatePlayer();
    }

    public static TextBlock Text(string text, double size = 14, bool bold = false, string color = "#EFF6F0", Thickness? margin = null) => new()
    { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = Brush(color), Margin = margin ?? new Thickness(), TextWrapping = TextWrapping.Wrap };

    public static Button Button(string label, Action action, bool accent = false)
    {
        var button = new Button { Content = label };
        if (accent) { button.Background = Brush("#C2F278"); button.Foreground = Brush("#172216"); }
        button.Click += (_, _) => action(); return button;
    }
    private static VisualizerStyle ParseStyle(string value) => Enum.TryParse<VisualizerStyle>(value, true, out var style) ? style : VisualizerStyle.Bars;

    public static Border Card(UIElement content, string color = "#192527", Thickness? padding = null) => new()
    { Background = Brush(color), CornerRadius = new CornerRadius(12), Padding = padding ?? new Thickness(18), Child = content, Margin = new Thickness(0, 0, 0, 10) };

    private void UpdatePlayer()
    {
        var radio = app.Radio;
        stationTitle.Text = radio.Current?.Name ?? "Your next favorite frequency.";
        status.Text = radio.Status.ToUpperInvariant();
        track.Text = radio.Track;
        play.Content = radio.IsActive ? "Ⅱ  Pause" : "▶  Play";
        if ((int)volume.Value != app.Settings.Volume) volume.Value = app.Settings.Volume;
        volumeLabel.Text = $"{app.Settings.Volume}%";
        var next = radio.Next;
        upNext.Text = app.Settings.ScheduleEnabled ? next == null ? "SCHEDULE ON · Add your first time slot" : $"UP NEXT · {next.At:ddd HH:mm}  /  {app.Settings.Stations.FirstOrDefault(s => s.Id == next.Entry.StationId)?.Name}" : "SCHEDULE OFF · You're in control";
    }

    public void RefreshPage()
    {
        page.Children.Clear();
        for (var i = 0; i < navigation.Length; i++) { navigation[i].Background = Brush(i == tab ? "#C2F278" : "#293638"); navigation[i].Foreground = Brush(i == tab ? "#172216" : "#B4C3C3"); }
        if (tab == 0) ShowStations(); else if (tab == 1) ShowEditStations(); else if (tab == 2) ShowSchedule(); else ShowSettings();
        UpdatePlayer();
    }

    internal void SelectPage(int index) { tab = index; RefreshPage(); }

    private const double MinTileWidth = 300;

    private void ShowStations()
    {
        pageTitle.Text = "Your stations"; pageDescription.Text = "A few good frequencies. Always within reach.";
        if (app.Settings.Stations.Count == 0)
        {
            page.Children.Add(Card(Text("Start with a station you love. Add its direct MP3, AAC or HLS stream URL in Edit stations.", 16)));
            return;
        }
        // Tiles flow left to right and wrap; ItemWidth is recomputed so each row fills the available width evenly.
        var tiles = new WrapPanel { Margin = new Thickness(0, 0, -10, 0) };
        tiles.SizeChanged += (_, e) => tiles.ItemWidth = e.NewSize.Width / Math.Max(1, Math.Floor(e.NewSize.Width / MinTileWidth));
        foreach (var station in app.Settings.Stations) tiles.Children.Add(StationTile(station));
        page.Children.Add(tiles);
    }

    private Border StationTile(Station station)
    {
        const string rest = "#192527", hover = "#2B4232";
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(StationBadge(station));
        var info = StationInfo(station, false); Grid.SetColumn(info, 1); row.Children.Add(info);
        var playChip = new Border { CornerRadius = new CornerRadius(8), Background = Brush("#C2F278"), Padding = new Thickness(12, 7, 12, 7), VerticalAlignment = VerticalAlignment.Center, Opacity = 0, Child = Text("▶  Play", 12, true, "#172216") };
        Grid.SetColumn(playChip, 2); row.Children.Add(playChip);
        var tile = new Border { Background = Brush(rest), CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Margin = new Thickness(0, 0, 10, 10), Child = row, Cursor = System.Windows.Input.Cursors.Hand, Focusable = true };
        AutomationProperties.SetName(tile, "Play " + station.Name);
        void Highlight(bool on) { tile.Background = Brush(on ? hover : rest); playChip.Opacity = on ? 1 : 0; }
        void Play() { app.Radio.Play(station); app.Save(); }
        tile.MouseEnter += (_, _) => Highlight(true);
        tile.MouseLeave += (_, _) => Highlight(tile.IsKeyboardFocused);
        tile.GotKeyboardFocus += (_, _) => Highlight(true);
        tile.LostKeyboardFocus += (_, _) => Highlight(tile.IsMouseOver);
        tile.MouseLeftButtonUp += (_, _) => Play();
        tile.KeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space) { Play(); e.Handled = true; } };
        return tile;
    }

    private static Border StationBadge(Station station)
    {
        var initial = Text(station.Name[..1].ToUpperInvariant(), 24, true, "#C2F278"); initial.HorizontalAlignment = HorizontalAlignment.Center; initial.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(10), Background = Brush("#30413A"), Child = initial, HorizontalAlignment = HorizontalAlignment.Left };
    }

    private StackPanel StationInfo(Station station, bool showFallback)
    {
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var name = Text(station.Name, 17, true); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; info.Children.Add(name);
        var tag = Text(station.Tag + (showFallback && app.Settings.FallbackStationId == station.Id ? " · Fallback" : ""), 12, false, "#9BB0B2", new Thickness(0, 4, 0, 0)); tag.TextWrapping = TextWrapping.NoWrap; tag.TextTrimming = TextTrimming.CharacterEllipsis; info.Children.Add(tag);
        return info;
    }

    private void ShowEditStations()
    {
        pageTitle.Text = "Edit stations"; pageDescription.Text = "Add new frequencies or change the ones you have.";
        var tools = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var add = Button("+  Add station", () => EditStation(null), true); DockPanel.SetDock(add, Dock.Right); tools.Children.Add(add);
        tools.Children.Add(Text($"{app.Settings.Stations.Count:00}  SAVED FREQUENCIES", 11, true, "#81989A", new Thickness(0, 12, 0, 0))); page.Children.Add(tools);
        foreach (var station in app.Settings.Stations)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(StationBadge(station));
            var info = StationInfo(station, true); Grid.SetColumn(info, 1); row.Children.Add(info);
            var edit = Button("Edit", () => EditStation(station)); edit.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(edit, 2); row.Children.Add(edit); page.Children.Add(Card(row));
        }
        if (app.Settings.Stations.Count == 0) page.Children.Add(Card(Text("Start with a station you love. Add its direct MP3, AAC or HLS stream URL above.", 16)));
        page.Children.Add(Text("Starter stations by SomaFM. Add your Greek favorites with their direct stream URLs.", 12, false, "#81989A", new Thickness(0, 6, 0, 8)));
    }

    private void EditStation(Station? station)
    {
        var editor = new StationDialog(app, station) { Owner = this };
        if (editor.ShowDialog() == true) app.Refresh();
    }

    private void ShowSchedule()
    {
        pageTitle.Text = "Make radio a routine"; pageDescription.Text = "Each slot switches station at its start time. It plays until the next switch.";
        var tools = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var add = Button("+  Add time slot", () => EditSlot(null), true); add.IsEnabled = app.Settings.Stations.Count > 0; DockPanel.SetDock(add, Dock.Right); tools.Children.Add(add);
        var enabled = new CheckBox { Content = "Follow my schedule", IsChecked = app.Settings.ScheduleEnabled };
        enabled.Click += (_, _) => { app.Settings.ScheduleEnabled = enabled.IsChecked == true; app.Radio.RefreshSchedule(); app.Refresh(); };
        tools.Children.Add(enabled); page.Children.Add(tools);
        var days = new UniformGridShim();
        for (var i = 0; i < Week.Length; i++)
        {
            var index = i; var day = Button(Week[i].ToString()[..3], () => { selectedDay = index; RefreshPage(); }, selectedDay == i); days.Children.Add(day);
        }
        page.Children.Add(days);
        var slots = app.Settings.Schedule.Where(e => e.Days.Contains(Week[selectedDay])).OrderBy(e => e.Time).ToList();
        foreach (var entry in slots)
        {
            var station = app.Settings.Stations.FirstOrDefault(s => s.Id == entry.StationId);
            var row = new DockPanel(); var edit = Button("Edit", () => EditSlot(entry)); DockPanel.SetDock(edit, Dock.Right); row.Children.Add(edit);
            var time = Text(entry.Time, 24, true, entry.Enabled ? "#C2F278" : "#81989A", new Thickness(0, 0, 24, 0)); time.Width = 100; DockPanel.SetDock(time, Dock.Left); row.Children.Add(time);
            var info = new StackPanel(); info.Children.Add(Text(station?.Name ?? "Missing station", 17, true));
            info.Children.Add(Text((entry.Enabled ? (string.IsNullOrWhiteSpace(entry.Label) ? "Scheduled switch" : entry.Label) : "Disabled") + " · " + string.Join(", ", Week.Where(entry.Days.Contains).Select(d => d.ToString()[..3])), 12, false, "#9BB0B2", new Thickness(0, 4, 0, 0)));
            row.Children.Add(info); page.Children.Add(Card(row));
        }
        if (slots.Count == 0) page.Children.Add(Card(new StackPanel { Children = { Text("A little room for spontaneity.", 19, true), Text("No switches on " + Week[selectedDay] + ". Add a time slot to tune in automatically.", 13, false, "#9BB0B2", new Thickness(0, 8, 0, 0)) } }, padding: new Thickness(24)));
        page.Children.Add(Text("Times follow your Windows time zone. Pause or pick a station manually until the next slot. After sleep, DialShift catches up with the current slot.", 12, false, "#81989A", new Thickness(0, 8, 0, 0)));
    }

    private void EditSlot(ScheduleEntry? entry)
    {
        var editor = new ScheduleDialog(app, entry, Week[selectedDay]) { Owner = this };
        if (editor.ShowDialog() == true) { app.Radio.RefreshSchedule(); app.Refresh(); }
    }

    private void ShowSettings()
    {
        pageTitle.Text = "Set it. Forget it."; pageDescription.Text = "Small preferences for your daily listening.";
        var startup = new StackPanel(); startup.Children.Add(Text("At your service", 18, true));
        var login = new CheckBox { Content = "Launch DialShift in the tray when I sign in to Windows", IsChecked = app.Settings.LaunchAtLogin };
        login.Click += (_, _) => { try { app.SetStartup(login.IsChecked == true); } catch (Exception ex) { login.IsChecked = app.Settings.LaunchAtLogin; MessageBox.Show(this, ex.Message, "Couldn't update startup"); } };
        startup.Children.Add(login);
        var hidden = new CheckBox { Content = "Start in the tray when opened normally", IsChecked = app.Settings.StartInTray };
        hidden.Click += (_, _) => { app.Settings.StartInTray = hidden.IsChecked == true; app.Save(); }; startup.Children.Add(hidden);
        startup.Children.Add(Text("Closing the window keeps your radio running. Choose Quit DialShift in the tray to exit.", 12, false, "#9BB0B2")); page.Children.Add(Card(startup));
        var recovery = new StackPanel(); recovery.Children.Add(Text("Keep the music going", 18, true));
        recovery.Children.Add(Text("Retry a failed stream, then use this station as a fallback. Try the original again every 2 minutes.", 12, false, "#9BB0B2", new Thickness(0, 7, 0, 6)));
        var fallback = new ComboBox { MaxWidth = 380, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 280 };
        fallback.Items.Add(new Station { Id = Guid.Empty, Name = "No fallback · keep retrying" });
        foreach (var station in app.Settings.Stations) fallback.Items.Add(station);
        fallback.SelectedItem = fallback.Items.Cast<Station>().FirstOrDefault(s => s.Id == app.Settings.FallbackStationId) ?? fallback.Items[0];
        fallback.SelectionChanged += (_, _) => { app.Settings.FallbackStationId = fallback.SelectedItem is Station s && s.Id != Guid.Empty ? s.Id : null; app.Save(); }; recovery.Children.Add(fallback); page.Children.Add(Card(recovery));
        var about = new StackPanel(); about.Children.Add(Text("DialShift  /  0.2.0", 16, true));
        about.Children.Add(Text("Your stations. Your schedule. Stored on this computer.", 13, false, "#9BB0B2", new Thickness(0, 6, 0, 14)));
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Button("Open settings folder ↗", () => Process.Start(new ProcessStartInfo("explorer.exe", app.Store.DirectoryPath) { UseShellExecute = true })));
        var export = Button("Export stations & schedule…", ExportSettings); export.Margin = new Thickness(10, 0, 0, 0); actions.Children.Add(export);
        about.Children.Add(actions);
        page.Children.Add(Card(about));
    }

    private void ExportSettings()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export DialShift settings", FileName = "DialShift-settings.json", DefaultExt = ".json", Filter = "DialShift settings (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { app.Store.Save(app.Settings); System.IO.File.Copy(app.Store.FilePath, dialog.FileName, true); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Couldn't export settings"); }
    }

    private sealed class UniformGridShim : System.Windows.Controls.Primitives.UniformGrid
    {
        public UniformGridShim() { Rows = 1; Columns = 7; Margin = new Thickness(0, 0, 0, 15); }
    }
}
