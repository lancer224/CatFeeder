using System.Windows;
using System.Windows.Controls;

namespace CatFeeder
{
    public static class Prompt
    {
        public static string Show(string title, string label, string defaultValue = "")
        {
            var win = CreateWindow(title, 180);
            var grid = new Grid { Margin = new Thickness(12) };
            for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(lbl, 0);

            var tb = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 8), FontSize = 14 };
            Grid.SetRow(tb, 1);
            tb.SelectAll();

            var buttons = MakeButtons(win, out var ok, out var cancel);
            Grid.SetRow(buttons, 2);
            ok.Click += (_, _) => win.DialogResult = true;

            grid.Children.Add(lbl);
            grid.Children.Add(tb);
            grid.Children.Add(buttons);
            win.Content = grid;
            tb.Focus();
            return win.ShowDialog() == true ? tb.Text : null;
        }

        public static int Choice(string title, string label, string[] options)
        {
            var win = CreateWindow(title, 280);
            var grid = new Grid { Margin = new Thickness(12) };
            for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 5) };
            Grid.SetRow(lbl, 0);

            var list = new ListBox { Height = 140, Margin = new Thickness(0, 0, 0, 8), FontSize = 14 };
            list.ItemsSource = options;
            if (options.Length > 0) list.SelectedIndex = 0;
            Grid.SetRow(list, 1);

            var buttons = MakeButtons(win, out var ok, out _);
            Grid.SetRow(buttons, 2);
            ok.Click += (_, _) => win.DialogResult = true;
            list.MouseDoubleClick += (_, _) => { win.DialogResult = true; };

            grid.Children.Add(lbl);
            grid.Children.Add(list);
            grid.Children.Add(buttons);
            win.Content = grid;
            return win.ShowDialog() == true ? list.SelectedIndex : -1;
        }

        private static Window CreateWindow(string title, int height) => new Window
        {
            Title = title,
            Width = 420,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = System.Windows.Application.Current.MainWindow,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            FontSize = 14
        };

        private static StackPanel MakeButtons(Window win, out Button ok, out Button cancel)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            ok = new Button { Content = "ОК", Width = 80, IsDefault = true, Margin = new Thickness(4) };
            cancel = new Button { Content = "Отмена", Width = 80, IsCancel = true, Margin = new Thickness(4) };
            cancel.Click += (_, _) => win.DialogResult = false;
            panel.Children.Add(ok);
            panel.Children.Add(cancel);
            return panel;
        }
    }
}