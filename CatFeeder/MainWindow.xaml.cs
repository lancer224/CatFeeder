using CatFeeder.Application;
using CatFeeder.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CatFeeder
{
    public partial class MainWindow : Window
    {
        private readonly User _user = new User { Name = "Владелец" };
        private readonly FeederService _fs;
        private readonly SimulationService _sim;
        private readonly DispatcherTimer _uiTimer;

        private readonly ObservableCollection<FeederRow> _feederRows = new ObservableCollection<FeederRow>();
        private readonly ObservableCollection<CatRow> _catRows = new ObservableCollection<CatRow>();
        private readonly ObservableCollection<FeedingRow> _feedingRows = new ObservableCollection<FeedingRow>();
        private readonly Queue<string> _catEvents = new Queue<string>();
        private Guid? _lastFeedingsScheduleId;

        public MainWindow()
        {
            InitializeComponent();

            _fs = new FeederService(_user);
            _sim = new SimulationService(_fs);
            _sim.CatEvent += e => { lock (_catEvents) _catEvents.Enqueue(e); };

            FeedersGrid.ItemsSource = _feederRows;
            CatsGrid.ItemsSource = _catRows;
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
            RefreshCats();

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
                $" Кормушек: {_fs.Feeders.Count}   |   " +
                $"Всего выдано: {_fs.TotalDispensed:F1} г   |   " +
                $"Симуляция: {(_sim.IsRunning ? "РАБОТАЕТ" : "ПАУЗА")}   |   " +
                $"Время: {_sim.SimulatedNow:yyyy-MM-dd HH:mm:ss}";
        }

        private void RefreshFeeders()
        {
            var selectedId = (FeedersGrid.SelectedItem as FeederRow)?.Feeder.Id;
            _feederRows.Clear();

            foreach (var f in _fs.Feeders)
            {
                var schedName = f.ScheduleId != null
                    ? _fs.Schedules.FirstOrDefault(s => s.Id == f.ScheduleId.Value)?.Name ?? "?"
                    : "-";
                _feederRows.Add(new FeederRow(f, schedName));
            }

            // Сохраняем выделение кормушки
            if (selectedId != null)
            {
                foreach (var row in _feederRows)
                {
                    if (row.Feeder.Id == selectedId)
                    {
                        FeedersGrid.SelectedItem = row;
                        break;
                    }
                }
            }
        }

        private void RefreshCats()
        {
            var selectedId = (CatsGrid.SelectedItem as CatRow)?.Cat.Id;
            _catRows.Clear();

            foreach (var c in _fs.Cats)
            {
                var feeder = c.AssignedFeederId != null ? _fs.Find(c.AssignedFeederId.Value) : null;
                _catRows.Add(new CatRow(c, feeder != null ? feeder.Name : "<Любая>"));
            }

            // Сохраняем выделение кота
            if (selectedId != null)
            {
                foreach (var row in _catRows)
                {
                    if (row.Cat.Id == selectedId)
                    {
                        CatsGrid.SelectedItem = row;
                        break;
                    }
                }
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
                $"Симуляция: {(_sim.IsRunning ? "РАБОТАЕТ" : "ПАУЗА")}   |   " +
                $"Множитель: {_sim.TimeMultiplier:F0}x   |   " +
                $"Время: {_sim.SimulatedNow:yyyy-MM-dd HH:mm:ss}";

            CatStatusText.Text = $"Котов в системе: {_fs.Cats.Count}";
        }

        // ============================================================
        //  HELPERS ДЛЯ ВЫБОРА
        // ============================================================
        private bool TryGetSelectedCat(out Cat c)
        {
            c = null;
            if (CatsGrid.SelectedItem is CatRow row)
            {
                c = row.Cat;
                return true;
            }
            MessageBox.Show("Пожалуйста, сначала выберите кота в таблице.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private bool TryGetSelectedFeeder(out Feeder f)
        {
            f = null;
            if (FeedersGrid.SelectedItem is FeederRow row)
            {
                f = row.Feeder;
                return true;
            }
            MessageBox.Show("Пожалуйста, сначала выберите кормушку в таблице.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // ============================================================
        //  CATS ACTIONS
        // ============================================================
        private void AddCat_Click(object sender, RoutedEventArgs e)
        {
            var name = Prompt.Show("Новый кот", "Кличка:", "Мурзик");
            if (name != null) { _fs.AddCat(name); RefreshAll(); }
        }

        private void RemoveCat_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedCat(out var c)) return;
            _fs.RemoveCat(c.Id);
            RefreshAll();
        }

        private void EditCatHunger_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedCat(out var c)) return;
            var str = Prompt.Show("Голод", "Уровень голода (0-100):", c.Hunger.ToString("F0"));
            if (double.TryParse(str, out var h)) c.Hunger = Math.Max(0, Math.Min(100, h));
            RefreshAll();
        }

        private void EditCatThreshold_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedCat(out var c)) return;
            var str = Prompt.Show("Порог еды", "При каком голоде кот идет есть (0-100):", c.EatThreshold.ToString("F0"));
            if (double.TryParse(str, out var t)) c.EatThreshold = Math.Max(0, Math.Min(100, t));
            RefreshAll();
        }

        private void EditCatWaterRate_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedCat(out var c)) return;
            var str = Prompt.Show("Расход воды", "Сколько мл воды тратится на 1 г корма:", c.WaterConsumptionRate.ToString("F1"));
            if (double.TryParse(str, out var r)) c.WaterConsumptionRate = Math.Max(0, r);
            RefreshAll();
        }

        private void AssignCatFeeder_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetSelectedCat(out var c)) return;
            var options = new List<string> { "<Любая кормушка>" };
            options.AddRange(_fs.Feeders.Select(f => f.Name));

            var picked = Prompt.Choice("Назначить кормушку", $"Для кота '{c.Name}'", options.ToArray());
            if (picked < 0) return;

            c.AssignedFeederId = picked == 0 ? (Guid?)null : _fs.Feeders[picked - 1].Id;
            RefreshAll();
        }

        // ============================================================
        //  FEEDERS ACTIONS
        // ============================================================
        private void AddDispenser_Click(object s, RoutedEventArgs e) => AddFeeder(FeederType.Dispenser);
        private void AddWeightSensor_Click(object s, RoutedEventArgs e) => AddFeeder(FeederType.WeightSensor);

        private void AddFeeder(FeederType type)
        {
            var name = Prompt.Show("Новая кормушка", "Название:", type == FeederType.Dispenser ? "Кухня" : "Гостиная");
            if (name == null) return;
            var capStr = Prompt.Show("Новая кормушка", "Объем (г):", "500");
            if (!double.TryParse(capStr, out var cap) || cap <= 0) cap = 500;
            _fs.AddFeeder(type, name, cap);
            RefreshAll();
        }

        private void RemoveFeeder_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            if (MessageBox.Show($"Удалить '{f.Name}'?", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _fs.RemoveFeeder(f.Id);
            RefreshAll();
        }

        private void RefillFood_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            var free = f.FoodCapacity - f.FoodAmount;
            var str = Prompt.Show("Добавить корм", $"Граммы (свободно: {free:F0}):", free.ToString("F0"));
            if (double.TryParse(str, out var g)) f.Refill(g);
            RefreshAll();
        }

        private void RefillWater_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            var str = Prompt.Show("Добавить воду", "Миллилитры:", "250");
            if (double.TryParse(str, out var ml)) _fs.RefillWater(f.Id, ml);
            RefreshAll();
        }

        private void FeedNow_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            string prompt = f.Type == FeederType.Dispenser
                ? "Сколько грамм насыпать в миску:"
                : "Сколько грамм ДОЛЖНО БЫТЬ в миске:";

            var str = Prompt.Show("Покормить", prompt, "30");
            if (double.TryParse(str, out var g))
                MessageBox.Show($"Результат: {_fs.Feed(f.Id, g)}", "Кормление");
            RefreshAll();
        }

        private void ViewLog_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            var text = string.Join("\n", f.Log.Select(l =>
                $"[{l.Time:yyyy-MM-dd HH:mm:ss}] [{l.Type,-10}] {l.Message}"));
            var win = new Window
            {
                Title = $"Лог — {f.Name}",
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
                    FontSize = 12,
                    TextWrapping = TextWrapping.NoWrap
                }
            };
            win.ShowDialog();
        }

        private void ExportLog_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить лог кормушки",
                Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"log_{f.Name.Replace(' ', '_')}_{DateTime.Now:yyyyMMddHHmmss}.txt"
            };
            if (dlg.ShowDialog() != true) return;

            _fs.ExportFeederLog(f, dlg.FileName);
            MessageBox.Show("Лог успешно сохранен в:\n" + dlg.FileName, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PostponeNext_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            var str = Prompt.Show("Отложить выдачу", "Задержка (в минутах):", "30");
            if (!int.TryParse(str, out var m)) return;
            var res = _fs.PostponeNearest(f.Id, _sim.SimulatedNow, TimeSpan.FromMinutes(m));
            MessageBox.Show(res ?? "Нет предстоящих выдач");
            RefreshAll();
        }

        private void CancelNext_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            MessageBox.Show(_fs.CancelNearest(f.Id, _sim.SimulatedNow) ?? "Нет предстоящих выдач");
            RefreshAll();
        }

        private void AssignSchedule_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            if (_fs.Schedules.Count == 0) { MessageBox.Show("Нет доступных расписаний."); return; }
            var options = new List<string> { "<нет>" };
            options.AddRange(_fs.Schedules.Select(s => s.Name));
            var idx = Prompt.Choice("Назначить расписание", $"Кормушка '{f.Name}'", options.ToArray());
            if (idx < 0) return;
            _fs.AssignSchedule(f.Id, idx == 0 ? (Guid?)null : _fs.Schedules[idx - 1].Id);
            RefreshAll();
        }

        private void EditLabels_Click(object s, RoutedEventArgs e)
        {
            if (!TryGetSelectedFeeder(out var f)) return;
            var str = Prompt.Show("Изменить метки", "Метки через запятую:", string.Join(", ", f.Labels));
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
            var name = Prompt.Show("Новое расписание", "Название:", "Сухой корм");
            if (name == null) return;
            _fs.AddSchedule(name);
            RefreshAll();
        }

        private void RemoveSchedule_Click(object s, RoutedEventArgs e)
        {
            if (SchedulesList.SelectedItem is not Schedule sch) return;
            if (MessageBox.Show($"Удалить '{sch.Name}'?", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _fs.RemoveSchedule(sch.Id);
            _lastFeedingsScheduleId = null;
            RefreshAll();
        }

        private void ExportSchedule_Click(object s, RoutedEventArgs e)
        {
            if (SchedulesList.SelectedItem is not Schedule sch) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить расписание",
                Filter = "JSON (*.json)|*.json",
                FileName = $"schedule_{sch.Name}.json"
            };
            if (dlg.ShowDialog() != true) return;
            _fs.ExportSchedule(sch.Id, dlg.FileName);
            MessageBox.Show("Расписание успешно экспортировано!", "Успех");
        }

        private void ImportSchedule_Click(object s, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Загрузить расписание", Filter = "JSON (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;
            var r = _fs.ImportSchedule(dlg.FileName);
            MessageBox.Show(r != null ? "Импортировано: " + r.Name : "Ошибка импорта");
            RefreshAll();
        }

        private void AddFeeding_Click(object s, RoutedEventArgs e)
        {
            if (SchedulesList.SelectedItem is not Schedule sch) return;
            var timeStr = Prompt.Show("Добавить кормление", "Время (ЧЧ:ММ):", "08:00");
            if (!TimeSpan.TryParse(timeStr, out var t)) return;

            string prompt = "Сколько грамм насыпать:";
            var feedersWithThisSchedule = _fs.Feeders.Where(x => x.ScheduleId == sch.Id).ToList();
            if (feedersWithThisSchedule.Any(x => x.Type == FeederType.WeightSensor))
                prompt = "Целевой вес в миске (г):";

            var gramsStr = Prompt.Show("Добавить кормление", prompt, "30");
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
            var str = Prompt.Show("Множитель времени", "720 = 1 час симуляции за 5 сек:", _sim.TimeMultiplier.ToString("F0"));
            if (double.TryParse(str, out var m) && m > 0) _sim.TimeMultiplier = m;
            RefreshAll();
        }

        // ============================================================
        //  SEED
        // ============================================================
        private void Seed()
        {
            var f1 = _fs.AddFeeder(FeederType.Dispenser, "Кухня", 500);
            f1.Labels.AddRange(new[] { "Барсик", "кухня" });

            var f2 = _fs.AddFeeder(FeederType.WeightSensor, "Загородный дом", 1000);
            f2.Labels.Add("дом");

            var dry = _fs.AddSchedule("Сухой корм");
            _fs.AddFeeding(dry.Id, TimeSpan.FromHours(8), 50);
            _fs.AddFeeding(dry.Id, TimeSpan.FromHours(18), 50);
            _fs.AssignSchedule(f1.Id, dry.Id);

            // Привязываем первого кота к Кухне
            var cat1 = _fs.AddCat("Барсик");
            cat1.AssignedFeederId = f1.Id;

            // Привязываем второго кота к Загородному дому
            var cat2 = _fs.AddCat("Мурка");
            cat2.AssignedFeederId = f2.Id;
        }
    }

    // ---------- ROW WRAPPERS ----------
    public class FeederRow
    {
        public Feeder Feeder { get; }
        public string ScheduleName { get; }

        public FeederRow(Feeder f, string schedName)
        {
            Feeder = f;
            ScheduleName = schedName;
        }

        public string Name => Feeder.Name;
        public string TypeName => Feeder.Type == FeederType.Dispenser ? "Дозатор" : "По весу";
        public string FoodText => $"{Feeder.FoodAmount:F1} / {Feeder.FoodCapacity:F0} г";
        public string BowlText => $"{Feeder.BowlAmount:F1} г";
        public string WaterText => $"{Feeder.WaterAmount:F0} мл";
        public string LabelsText => string.Join(", ", Feeder.Labels);
        public bool IsEmpty => Feeder.IsFoodEmpty;
    }

    public class CatRow
    {
        public Cat Cat { get; }
        public string FeederName { get; }

        public CatRow(Cat c, string feederName)
        {
            Cat = c;
            FeederName = feederName;
        }

        public string Name => Cat.Name;
        public string HungerText => $"{Cat.Hunger:F0} / 100";
        public string ThresholdText => $"{Cat.EatThreshold:F0}";
        public string WaterRateText => $"{Cat.WaterConsumptionRate:F1} мл";
    }

    public class FeedingRow
    {
        public ScheduledFeeding Feeding { get; }
        public FeedingRow(ScheduledFeeding sf) => Feeding = sf;

        public string TimeText => Feeding.TimeOfDay.ToString(@"hh\:mm");
        public string AmountText => $"{Feeding.AmountGrams} г";
    }
}