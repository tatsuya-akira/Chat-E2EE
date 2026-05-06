// Standardized to production level
// Purpose: Value converters for ChatWindow data bindings

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Hermes.Converters
{
    // ── Seen tick color: gray = sent, blue = seen ──────────────────────────────
    public class SeenColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSeen = value is bool b && b;
            return isSeen
                ? new SolidColorBrush(Color.FromRgb(59, 130, 246))   // #3B82F6 blue
                : new SolidColorBrush(Color.FromRgb(156, 163, 175));  // #9CA3AF gray
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    // ── Show Image control only when content is a URL ─────────────────────────
    public class IsImageUrlConverter : IValueConverter
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrEmpty(url)) return Visibility.Collapsed;
            string lower = url.ToLowerInvariant();
            bool isImage = lower.StartsWith("http") && Array.Exists(ImageExtensions, ext => lower.Contains(ext));
            return isImage ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    // ── Show Text control only when content is NOT a URL ──────────────────────
    public class IsNotImageUrlConverter : IValueConverter
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrEmpty(url)) return Visibility.Visible;
            string lower = url.ToLowerInvariant();
            bool isImage = lower.StartsWith("http") && Array.Exists(ImageExtensions, ext => lower.Contains(ext));
            return isImage ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    // ── Online status dot: green = online, gray = offline ─────────────────────
    public class BoolToOnlineColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool online = value is bool b && b;
            return online
                ? new SolidColorBrush(Color.FromRgb(16, 185, 129))   // #10B981 green
                : new SolidColorBrush(Color.FromRgb(156, 163, 175));  // #9CA3AF gray
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    // ── Show avatar Image only when AvatarUrl is non-empty ────────────────────
    public class HasAvatarToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool hasAvatar && hasAvatar ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    // ── Inverse: show initials ellipse only when no avatar ────────────────────
    public class NoAvatarToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool hasAvatar && hasAvatar ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
