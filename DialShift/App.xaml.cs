using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DialShift.Core;
using DialShift.Visualization;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DialShift;

public partial class App : Application
{
    public Settings Settings { get; private set; } = null!;
    public SettingsStore Store { get; private set; } = null!;
    public RadioController Radio { get; private set; } = null!;
    public AudioSpectrum Spectrum { get; } = new();
    private SystemAudioTap? audioTap;
    private Forms.NotifyIcon? tray;
    private Mutex? mutex;
    private EventWaitHandle? activation;
    private bool exiting;
    public bool SmokeTest { get; private set; }
    private static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DialShift");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SmokeTest = e.Args.Contains("--smoke-test");
        if (!SmokeTest)
        {
            mutex = new Mutex(true, "Local\\DialShift.App", out var created);
            if (!created)
            {
                try { EventWaitHandle.OpenExisting("Local\\DialShift.Activate").Set(); } catch (WaitHandleCannotBeOpenedException) { }
                Shutdown();
                return;
            }
            activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DialShift.Activate");
            var signal = activation;
            _ = Task.Run(() => { while (signal.WaitOne()) { if (exiting) break; Dispatcher.BeginInvoke(new Action(ShowWindow)); } });
        }
        try
        {
            Store = new SettingsStore(SmokeTest ? Path.Combine(Path.GetTempPath(), "DialShift-smoke-" + Guid.NewGuid()) : DataDirectory);
            Settings = Store.Load();
            if (SmokeTest) Settings.Volume = 0;
            Radio = new RadioController(Settings, Dispatcher);
            audioTap = new SystemAudioTap(Spectrum);
            audioTap.Start();
            MainWindow = new MainWindow(this);
            CreateTray();
            Radio.Changed += UpdateTray;
            SystemEvents.PowerModeChanged += PowerChanged;
            if (SmokeTest || !(e.Args.Contains("--tray") || Settings.StartInTray)) MainWindow.Show();
            Radio.StartSchedule();
            if (Store.Warning != null) MessageBox.Show(Store.Warning, "DialShift · Settings recovered");
            if (SmokeTest) _ = SmokeChecks.Run(this, e.Args);
        }
        catch (Exception ex)
        {
            Log(ex);
            MessageBox.Show("DialShift couldn't start. " + ex.Message + "\n\nDetails: " + Path.Combine(DataDirectory, "dialshift.log"), "DialShift");
            ExitApp();
        }
    }

    public void Save()
    {
        try { Store.Save(Settings); }
        catch (Exception ex)
        {
            Log(ex);
            MessageBox.Show("Couldn't save your changes: " + ex.Message, "DialShift · Save failed");
        }
    }

    public void Refresh()
    {
        Save();
        BuildTrayMenu();
        ((MainWindow)MainWindow).RefreshPage();
    }

    private void CreateTray()
    {
        var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/dialshift.ico"));
        using var source = resource.Stream;
        using var icon = new System.Drawing.Icon(source);
        tray = new Forms.NotifyIcon { Icon = (System.Drawing.Icon)icon.Clone(), Text = "DialShift · Ready", Visible = true };
        tray.DoubleClick += (_, _) => ShowWindow();
        BuildTrayMenu();
    }

    public void BuildTrayMenu()
    {
        if (tray == null) return;
        var old = tray.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open DialShift", null, (_, _) => ShowWindow());
        menu.Items.Add("Play / Pause", null, (_, _) => Radio.Toggle());
        menu.Items.Add("Next station", null, (_, _) => Radio.NextStation());
        var stations = new Forms.ToolStripMenuItem("Stations");
        foreach (var station in Settings.Stations) stations.DropDownItems.Add(station.Name, null, (_, _) => { Radio.Play(station); Save(); });
        menu.Items.Add(stations);
        var schedule = new Forms.ToolStripMenuItem("Follow schedule") { Checked = Settings.ScheduleEnabled };
        schedule.Click += (_, _) => { Settings.ScheduleEnabled = !Settings.ScheduleEnabled; Radio.RefreshSchedule(); Refresh(); };
        menu.Items.Add(schedule);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Volume +10", null, (_, _) => { Radio.SetVolume(Settings.Volume + 10); Save(); });
        menu.Items.Add("Volume −10", null, (_, _) => { Radio.SetVolume(Settings.Volume - 10); Save(); });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit DialShift", null, (_, _) => ExitApp());
        tray.ContextMenuStrip = menu;
        old?.Dispose();
    }

    private void UpdateTray()
    {
        if (tray == null) return;
        var text = $"DialShift · {(Radio.IsActive ? Radio.Current?.Name ?? "Connecting" : "Paused")}";
        tray.Text = text.Length > 63 ? text[..63] : text;
    }

    public void ShowWindow()
    {
        if (MainWindow == null) return;
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    public void HideToTray()
    {
        MainWindow.Hide();
        if (tray != null && !SmokeTest)
        {
            tray.BalloonTipTitle = "DialShift is in your tray";
            tray.BalloonTipText = "Your radio and schedule keep running. Right-click the dial icon to quit.";
            tray.ShowBalloonTip(2500);
        }
    }

    public void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("DialShift", $"\"{Environment.ProcessPath}\" --tray");
        else key.DeleteValue("DialShift", false);
        Settings.LaunchAtLogin = enabled;
        Save();
    }

    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(new Action(() => Radio.ResumeFromSleep()));
    }

    public void ExitApp()
    {
        if (exiting) return;
        exiting = true;
        SystemEvents.PowerModeChanged -= PowerChanged;
        if (Settings != null && Store != null) Save();
        Radio?.Dispose();
        audioTap?.Dispose();
        if (tray != null) { tray.Visible = false; tray.Icon?.Dispose(); tray.Dispose(); }
        activation?.Set();
        mutex?.Dispose();
        Shutdown();
    }

    public static void Log(Exception ex)
    {
        try { Directory.CreateDirectory(DataDirectory); File.AppendAllText(Path.Combine(DataDirectory, "dialshift.log"), $"{DateTime.Now:O} {ex}\n"); } catch { }
    }
}
