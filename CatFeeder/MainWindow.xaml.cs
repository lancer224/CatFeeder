using CatFeeder.Application;
using CatFeeder.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CatFeeder;

public partial class MainWindow : Window
{
    private readonly User _user = new() { Name = "Owner" };
    private readonly FeederService _fs;
    private readonly SimulationService _sim;
    private readonly DispatcherTimer _uiTimer;

    private readonly ObservableCollection<FeederRow> _feederRows = new();
    private readonly ObservableCollection<FeedingRow> _feedingRows = new();
    private readonly Queue<string> _catEvents = new();
    private Guid? _lastFeedingsScheduleId;

    public MainWindow()
    {
        InitializeComponent();

        _fs = new FeederService(_user);
        _sim = new SimulationService(_fs, new CatSimulator { Name = "Barsik" });
        _sim.CatEvent += e => { lock (_catEvents) _catEvents.Enqueue(e); };

        FeedersGrid.ItemsSource = _feederRows;
        FeedingsGrid.ItemsSource = _feedingRows;

        Seed();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (_, _) => RefreshAll();
        _uiTimer.Start();

        _sim.Start();
        RefreshAll();
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiTimer.Stop();
        _sim.Stop();
        base.OnClosed(e);
    }

    // ============================================================
    //  REFRESH
    // ============================================================
    private void RefreshAll()
    {
        RefreshFeeders();
        RefreshSchedules();
        RefreshFeedings();
        RefreshSimulation();

        lock (_catEvents)
        {
            while (_catEvents.Count > 0)
            {
                CatLogList.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {_catEvents.Dequeue()}");
                if (CatLogList.Items.Count > 300)
                    CatLogList.Items.RemoveAt(CatLogList.Items.Count - 1);
            }
        }

        StatusText.Text =
            $" Feeders: {_fs.Feeders.Count}   |   " +
            $"Total dispensed: {_fs.TotalDispensed:F1} g   |   " +
            $"Sim time: {_sim.SimulatedNow:yyyy-MM-dd HH:mm:ss}   |   " +
            $"Sim: {(_sim.IsRunning ? "RUNNING" : "PAUSED")}   |   " +
            $"Cat hunger: {_sim.Cat.Hunger:F0}/100";
    }

    private void RefreshFeeders()
    {
        var selectedId = (FeedersGrid.SelectedItem as FeederRow)?.Feeder.Id;
        _feederRows.Clear();
        foreach (var f in _fs.Feeders)
        {
            var schedName = f.ScheduleId is Guid sid
                ? _fs.Schedules.FirstOrDefault(s => s.Id == sid)?.Name ?? "?"
                : "-";
            _feederRows.Add(new FeederRow(f, schedName));
            if (f.Id == selectedId) FeedersGrid.SelectedItem = _feederRows[_feederRows.Count - 1];
        }
    }

    private void RefreshSchedules()
    {
        var selectedId = (SchedulesList.SelectedItem as Schedule)?.Id;
        var newIds = _fs.Schedules.Select(s => s.Id).ToList();
        var curIds = SchedulesList.Items.Cast<Schedule>().Select(s => s.Id).ToList();
        if (curIds.SequenceEqual(newIds)) return;

        SchedulesList.ItemsSource = null;
        SchedulesList.ItemsSource = _fs.Schedules.ToList();
        if (selectedId.HasValue)
        {
            foreach (var s in SchedulesList.Items.Cast<Schedule>())
                if (s.Id == selectedId.Value) { SchedulesList.SelectedItem = s; break; }
        }
    }

    private void RefreshFeedings()
    {
        var sch = SchedulesList.SelectedItem as Schedule;
        if (sch?.Id == _lastFeedingsScheduleId) return;
        _lastFeedingsScheduleId = sch?.Id;

        _feedingRows.Clear();
        if (sch != null)
            foreach (var sf in sch.Feedings)
                _feedingRows.Add(new FeedingRow(sf));
    }

    private void RefreshSimulation()
    {
        SimStatusText.Text =
            $"Simulation: {(_sim.IsRunning ? "RUNNING" : "PAUSED")}   |   " +
            $"Multiplier: {_sim.TimeMultiplier:F0}x   |   " +
            $"Sim now: {_sim.SimulatedNow:yyyy-MM-dd HH:mm:ss}";
        CatStatusText.Text =
            $"Cat '{_sim.Cat.Name}': hunger {_sim.Cat.Hunger:F0}/100 " +
            $"(eats at {_sim.Cat.EatThreshold:F0}+)";
    }

    // ============================================================
    //  FEEDERS ACTIONS
    // ============================================================
    private Feeder? SelectedFeeder() => (FeedersGrid.SelectedItem as FeederRow)?.Feeder;

    private void AddDispenser_Click(object s, RoutedEventArgs e) => AddFeeder(FeederType.Dispenser);
    private void AddWeightSensor_Click(object s, RoutedEventArgs e) => AddFeeder(FeederType.WeightSensor);

    private void AddFeeder(FeederType type)
    {
        var name = Prompt.Show("New feeder", "Name:", type == FeederType.Dispenser ? "Kitchen" : "Living room");
        if (name == null) return;
        var capStr = Prompt.Show("New feeder", "Capacity (g):", "500");
        if (!double.TryParse(capStr, out var cap) || cap <= 0) cap = 500;
        _fs.AddFeeder(type, name, cap);
        RefreshAll();
    }

    private void RemoveFeeder_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        if (MessageBox.Show($"Remove '{f.Name}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _fs.RemoveFeeder(f.Id);
        RefreshAll();
    }

    private void RefillFood_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        var free = f.FoodCapacity - f.FoodAmount;
        var str = Prompt.Show("Refill food", $"Grams (free: {free:F0}):", free.ToString("F0"));
        if (double.TryParse(str, out var g)) f.Refill(g);
        RefreshAll();
    }

    private void RefillWater_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        var str = Prompt.Show("Refill water", "Milliliters:", "250");
        if (double.TryParse(str, out var ml)) _fs.RefillWater(f.Id, ml);
        RefreshAll();
    }

    private void FeedNow_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        var str = Prompt.Show("Feed now", "Grams:", "30");
        if (double.TryParse(str, out var g))
            MessageBox.Show($"Result: {_fs.Feed(f.Id, g)}", "Feeding");
        RefreshAll();
    }

    private void ViewLog_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        var text = string.Join("\n", f.Log.Select(l =>
            $"[{l.Time:yyyy-MM-dd HH:mm:ss}] [{l.Type,-10}] {l.Message}"));
        var win = new Window
        {
            Title = $"Log — {f.Name}",
            Width = 900,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Content = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.NoWrap
            }
        };
        win.ShowDialog();
    }

    private void ExportLog_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text (*.txt)|*.txt",
            FileName = $"log_{f.Name.Replace(' ', '_')}_{DateTime.Now:yyyyMMddHHmmss}.txt"
        };
        if (dlg.ShowDialog() != true) return;
        _fs.ExportFeederLog(f, dlg.FileName);
        MessageBox.Show("Exported to " + dlg.FileName);
    }

    private void PostponeNext_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        var str = Prompt.Show("Postpone next", "Delay (minutes):", "30");
        if (!int.TryParse(str, out var m)) return;
        var res = _fs.PostponeNearest(f.Id, _sim.SimulatedNow, TimeSpan.FromMinutes(m));
        MessageBox.Show(res ?? "No upcoming feedings");
        RefreshAll();
    }

    private void CancelNext_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        MessageBox.Show(_fs.CancelNearest(f.Id, _sim.SimulatedNow) ?? "No upcoming feedings");
        RefreshAll();
    }

    private void AssignSchedule_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        if (_fs.Schedules.Count == 0) { MessageBox.Show("No schedules."); return; }
        var options = new List<string> { "<none>" };
        options.AddRange(_fs.Schedules.Select(s => s.Name));
        var idx = Prompt.Choice("Assign schedule", $"Feeder '{f.Name}'", options.ToArray());
        if (idx < 0) return;
        _fs.AssignSchedule(f.Id, idx == 0 ? null : _fs.Schedules[idx - 1].Id);
        RefreshAll();
    }

    private void EditLabels_Click(object s, RoutedEventArgs e)
    {
        var f = SelectedFeeder(); if (f == null) return;
        var str = Prompt.Show("Edit labels", "Comma-separated labels:", string.Join(", ", f.Labels));
        if (str == null) return;
        f.Labels.Clear();
        f.Labels.AddRange(str.Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x)));
        RefreshAll();
    }

    // ============================================================
    //  SCHEDULES ACTIONS
    // ============================================================
    private void SchedulesList_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        _lastFeedingsScheduleId = null;
        RefreshFeedings();
    }

    private void AddSchedule_Click(object s, RoutedEventArgs e)
    {
        var name = Prompt.Show("New schedule", "Name:", "Dry food");
        if (name == null) return;
        _fs.AddSchedule(name);
        RefreshAll();
    }

    private void RemoveSchedule_Click(object s, RoutedEventArgs e)
    {
        if (SchedulesList.SelectedItem is not Schedule sch) return;
        if (MessageBox.Show($"Remove '{sch.Name}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _fs.RemoveSchedule(sch.Id);
        _lastFeedingsScheduleId = null;
        RefreshAll();
    }

    private void ExportSchedule_Click(object s, RoutedEventArgs e)
    {
        if (SchedulesList.SelectedItem is not Schedule sch) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"schedule_{sch.Name}.json"
        };
        if (dlg.ShowDialog() != true) return;
        _fs.ExportSchedule(sch.Id, dlg.FileName);
        MessageBox.Show("Exported");
    }

    private void ImportSchedule_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dlg.ShowDialog() != true) return;
        var r = _fs.ImportSchedule(dlg.FileName);
        MessageBox.Show(r != null ? "Imported: " + r.Name : "Import failed");
        RefreshAll();
    }

    private void AddFeeding_Click(object s, RoutedEventArgs e)
    {
        if (SchedulesList.SelectedItem is not Schedule sch) return;
        var timeStr = Prompt.Show("Add feeding", "Time (HH:mm):", "08:00");
        if (!TimeSpan.TryParse(timeStr, out var t)) return;
        var gramsStr = Prompt.Show("Add feeding", "Amount (g):", "30");
        if (!double.TryParse(gramsStr, out var g)) return;
        _fs.AddFeeding(sch.Id, t, g);
        _lastFeedingsScheduleId = null;
        RefreshFeedings();
    }

    private void RemoveFeeding_Click(object s, RoutedEventArgs e)
    {
        if (SchedulesList.SelectedItem is not Schedule sch) return;
        if (FeedingsGrid.SelectedItem is not FeedingRow row) return;
        _fs.RemoveFeeding(sch.Id, row.Feeding.Id);
        _lastFeedingsScheduleId = null;
        RefreshFeedings();
    }

    // ============================================================
    //  SIMULATION ACTIONS
    // ============================================================
    private void SimStart_Click(object s, RoutedEventArgs e) { _sim.Start(); RefreshAll(); }
    private void SimPause_Click(object s, RoutedEventArgs e) { _sim.Stop(); RefreshAll(); }

    private void SimSetMultiplier_Click(object s, RoutedEventArgs e)
    {
        var str = Prompt.Show("Multiplier", "720 = 1h sim / 5s real:", _sim.TimeMultiplier.ToString("F0"));
        if (double.TryParse(str, out var m) && m > 0) _sim.TimeMultiplier = m;
        RefreshAll();
    }

    private void SimSetCatHunger_Click(object s, RoutedEventArgs e)
    {
        var str = Prompt.Show("Cat hunger", "0..100:", _sim.Cat.Hunger.ToString("F0"));
        if (double.TryParse(str, out var h)) _sim.Cat.Hunger = Math.Max(0, Math.Min(100, h));
        RefreshAll();
    }

    // ============================================================
    //  SEED
    // ============================================================
    private void Seed()
    {
        var f1 = _fs.AddFeeder(FeederType.Dispenser, "Kitchen feeder", 500);
        f1.Labels.AddRange(new[] { "Barsik", "kitchen" });

        var f2 = _fs.AddFeeder(FeederType.WeightSensor, "Country house", 1000);
        f2.Labels.Add("country house");

        var dry = _fs.AddSchedule("Dry food");
        _fs.AddFeeding(dry.Id, TimeSpan.FromHours(8), 50);
        _fs.AddFeeding(dry.Id, TimeSpan.FromHours(18), 50);
        _fs.AssignSchedule(f1.Id, dry.Id);
    }
}

// ---------- ROW WRAPPERS (для биндинга в DataGrid) ----------
public class FeederRow
{
    public Feeder Feeder { get; }
    public string ScheduleName { get; }
    public FeederRow(Feeder f, string schedName) { Feeder = f; ScheduleName = schedName; }

    public string Name => Feeder.Name;
    public string TypeName => Feeder.Type.ToString();
    public string FoodText => $"{Feeder.FoodAmount:F1} / {Feeder.FoodCapacity:F0} g";
    public string WaterText => $"{Feeder.WaterAmount:F0} ml";
    public string LabelsText => string.Join(", ", Feeder.Labels);
    public bool IsEmpty => Feeder.IsFoodEmpty;
}

public class FeedingRow
{
    public ScheduledFeeding Feeding { get; }
    public FeedingRow(ScheduledFeeding sf) => Feeding = sf;

    public string TimeText => Feeding.TimeOfDay.ToString(@"hh\:mm");
    public string AmountText => $"{Feeding.AmountGrams} g";
}