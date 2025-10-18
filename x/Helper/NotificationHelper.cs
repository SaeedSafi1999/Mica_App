using System;
using System.Windows.Forms;

namespace YourNamespace
{
    public static class WindowsNotifier
    {
        private static NotifyIcon _notifyIcon;

        static WindowsNotifier()
        {
            _notifyIcon = new NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Information 
            };
        }

        public static void ShowNotification(string title, string message, int duration = 3000)
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(duration); 
        }
    }
}
