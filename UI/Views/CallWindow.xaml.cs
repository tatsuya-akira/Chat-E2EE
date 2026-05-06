// Standardized to production level
// Purpose: CallWindow code-behind – shows call UI and listens for end-call signal

using System;
using System.Windows;

namespace Hermes
{
    public partial class CallWindow : Window
    {
        private readonly string _callType;
        private readonly string _callerName;

        public CallWindow(string callerId, string calleeName, string callType)
        {
            InitializeComponent();
            _callType   = callType;
            _callerName = calleeName;
            txtCallee.Text      = calleeName;
            txtCallStatus.Text  = callType == "video" ? "📹 Gọi video đang kết nối..." : "📞 Đang gọi...";
            Title = callType == "video" ? "Gọi Video" : "Gọi Thoại";
        }

        private void btnEndCall_Click(object sender, RoutedEventArgs e) => this.Close();

        private void btnMute_Click(object sender, RoutedEventArgs e)
        {
            // Toggle mute state indicator
            txtCallStatus.Text = txtCallStatus.Text.Contains("Tắt mic") ? "Đang kết nối..." : "🔇 Tắt mic";
        }

        private void btnSpeaker_Click(object sender, RoutedEventArgs e)
        {
            txtCallStatus.Text = "🔊 Loa ngoài bật";
        }
    }
}
