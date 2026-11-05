using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Eddy
{
    public class ContinueLogDialog : Window
    {
        public bool? Result { get; private set; }

        public ContinueLogDialog(string previousFileName, Window? owner = null)
        {
            Title = "Log Session";
            Width = 460;
            Height = 190;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            if (owner != null) Owner = owner;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var message = new TextBlock
            {
                Text = $"Configuration matches the previous file '{previousFileName}'.\n\nDo you want to continue logging to this file or start a new file?",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(message, 0);
            grid.Children.Add(message);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var btnContinue = new Button
            {
                Content = $"Continue {previousFileName}",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(48, 83, 113)),
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnContinue.Click += (s, e) =>
            {
                Result = true;
                DialogResult = true;
                Close();
            };

            var btnNewFile = new Button
            {
                Content = "New file",
                Padding = new Thickness(15, 8, 15, 8),
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromRgb(48, 83, 113)),
                Foreground = Brushes.White,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnNewFile.Click += (s, e) =>
            {
                Result = false;
                DialogResult = true;
                Close();
            };

            buttonPanel.Children.Add(btnContinue);
            buttonPanel.Children.Add(btnNewFile);
            Grid.SetRow(buttonPanel, 1);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }
    }
}
