using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace GlanceSearch.App.Views.Settings;

/// <summary>
/// A simple window to display the CHANGELOG.md content when the app updates.
/// </summary>
public class ChangelogWindow : Window
{
    public ChangelogWindow()
    {
        InitializeComponent();
        LoadChangelog();
    }

    private void InitializeComponent()
    {
        Title = "✨ What's New in GlanceSearch";
        Width = 600;
        Height = 450;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        Foreground = Brushes.White;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Scrollable content area
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(16)
        };

        _markdownText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            FontSize = 13,
            FontFamily = new FontFamily("Segoe UI")
        };
        scrollViewer.Content = _markdownText;
        Grid.SetRow(scrollViewer, 0);
        grid.Children.Add(scrollViewer);

        // Footer button
        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            Padding = new Thickness(16)
        };
        Grid.SetRow(footer, 1);
        grid.Children.Add(footer);

        var closeBtn = new Button
        {
            Content = "Awesome, let's go!",
            Width = 150,
            Height = 35,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand
        };
        closeBtn.Click += (_, _) => Close();
        footer.Child = closeBtn;

        Content = grid;
    }

    private TextBlock _markdownText = null!;

    private void LoadChangelog()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                // Basic cleanup of markdown for text block display
                content = content.Replace("## ", "• ").Replace("# ", "").Replace("**", "");
                _markdownText.Text = content;
            }
            else
            {
                _markdownText.Text = "Welcome to the latest version of GlanceSearch!";
            }
        }
        catch (Exception)
        {
            _markdownText.Text = "Welcome to the new update!";
        }
    }
}
