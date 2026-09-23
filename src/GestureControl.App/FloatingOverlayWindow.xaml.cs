using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GestureControl.Core.Models;
using GestureControl.Interop.Native;

namespace GestureControl.App;

public partial class FloatingOverlayWindow : Window
{
    private static readonly SolidColorBrush ActiveLedBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush InactiveLedBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush MouseOnBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush MouseOffBrush = new(Color.FromRgb(71, 85, 105));
    private static readonly SolidColorBrush TriggerBrush = new(Color.FromRgb(245, 158, 11));
    private static readonly SolidColorBrush DefaultTextBrush = new(Color.FromRgb(248, 250, 252));

    public FloatingOverlayWindow()
    {
        InitializeComponent();
        Left = SystemParameters.PrimaryScreenWidth - Width - 30;
        Top = 40;
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        Win32Input.MakeWindowClickThrough(handle);
    }

    public void UpdateState(
        HandGestureType gesture,
        float confidence,
        string profileName,
        bool isActive,
        ActionCommand? action,
        bool isConfirmed,
        bool isMouseTrackingActive = true)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => UpdateState(gesture, confidence, profileName, isActive, action, isConfirmed, isMouseTrackingActive));
            return;
        }

        LedActive.Fill = isActive ? ActiveLedBrush : InactiveLedBrush;
        TxtProfileName.Text = profileName;

        BadgeMouseTracking.Background = isMouseTrackingActive ? MouseOnBrush : MouseOffBrush;
        TxtMouseTracking.Text = isMouseTrackingActive ? "MOUSE ON" : "MOUSE OFF";

        TxtGestureIcon.Text = gesture switch
        {
            HandGestureType.OpenHand => "🖐️",
            HandGestureType.Fist => "✊",
            HandGestureType.Pinch => "👌",
            HandGestureType.IndexPoint => "👉",
            HandGestureType.TwoFingersPeace => "✌️",
            HandGestureType.LateralPalm => "🤚",
            HandGestureType.SwipeLeft => "👈",
            HandGestureType.SwipeRight => "👉",
            HandGestureType.SwipeUp => "👆",
            HandGestureType.SwipeDown => "👇",
            _ => "✋"
        };

        if (gesture == HandGestureType.None)
        {
            TxtGestureName.Text = "Esperando mano...";
            TxtGestureName.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            TxtActionName.Text = "Ninguna acción";
            PrgConfidence.Value = 0;
            TxtConfidence.Text = "0%";
        }
        else
        {
            string prefix = gesture.IsDynamic() ? "[Dinámico] " : "[Estático] ";
            TxtGestureName.Text = prefix + gesture.ToString();
            TxtGestureName.Foreground = isConfirmed ? TriggerBrush : DefaultTextBrush;

            TxtActionName.Text = action?.Description ?? (isConfirmed ? "Acción ejecutada" : "En curso...");
            int pct = (int)Math.Clamp(confidence * 100, 0, 100);
            PrgConfidence.Value = pct;
            TxtConfidence.Text = $"{pct}%";
        }
    }
}
