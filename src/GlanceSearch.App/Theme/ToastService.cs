using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace GlanceSearch.App.Theme;

public enum ToastType
{
    Success,
    Info,
    Warning,
    Error
}

/// <summary>
/// A lightweight floating notification window that appears near the system tray.
/// </summary>
public class ToastWindow : Window
{
    private readonly DispatcherTimer _timer;

    private readonly Action? _onClick;

    public ToastWindow(string title, string message, ToastType type, Action? onClick = null)
    {
        _onClick = onClick;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Width = 320;
        MinHeight = 85;
        SizeToContent = SizeToContent.Height;

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
            Margin = new Thickness(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 2,
                Opacity = 0.5
            }
        };

        var grid = new Grid { Margin = new Thickness(12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text

        var iconText = type switch
        {
            ToastType.Success => "✅",
            ToastType.Info => "ℹ️",
            ToastType.Warning => "⚠️",
            ToastType.Error => "❌",
            _ => "ℹ️"
        };

        var icon = new TextBlock
        {
            Text = iconText,
            FontSize = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            FontSize = 14
        });
        stack.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });

        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        border.Child = grid;
        Content = border;

        // Position bottom right of primary screen
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;

        Loaded += (_, _) => AnimateIn();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => AnimateOut();
        _timer.Start();

        // Pause timer on hover
        MouseEnter += (_, _) => _timer.Stop();
        MouseLeave += (_, _) => _timer.Start();
        
        // Dismiss on click, and run action if provided
        MouseDown += (_, _) =>
        {
            _onClick?.Invoke();
            AnimateOut();
        };
    }

    private void AnimateIn()
    {
        var slide = new DoubleAnimation
        {
            From = SystemParameters.WorkArea.Bottom + Height,
            To = Top,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(TopProperty, slide);
    }

    private void AnimateOut()
    {
        _timer.Stop();
        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200)
        };
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}

/// <summary>
/// Service to easily trigger toast notifications.
/// Automatically handles thread dispatching if called from background threads.
/// </summary>
public static class ToastService
{
    public static void Show(string title, string message, ToastType type = ToastType.Info, Action? onClick = null)
    {
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var toast = new ToastWindow(title, message, type, onClick);
                toast.Show();
            });
        }
    }
}
