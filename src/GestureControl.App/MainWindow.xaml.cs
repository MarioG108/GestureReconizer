using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using GestureControl.Actions.Dispatchers;
using GestureControl.Actions.Profiles;
using GestureControl.Core.Interfaces;
using GestureControl.Core.Models;
using GestureControl.Core.Pipeline;
using GestureControl.Gestures.Classifiers;
using GestureControl.Gestures.Engine;
using GestureControl.Gestures.Features;
using GestureControl.Gestures.Filters;
using GestureControl.Infrastructure.Persistence;
using GestureControl.Interop.Native;
using GestureControl.Vision.Camera;
using GestureControl.Vision.Tracking;

namespace GestureControl.App;

public partial class MainWindow : Window
{
    private readonly ICameraService _cameraService;
    private readonly IHandTracker _handTracker;
    private readonly ITemporalFilter _temporalFilter;
    private readonly IFeatureExtractor _featureExtractor;
    private readonly IGestureClassifier _classifier;
    private readonly IGestureEngine _gestureEngine;
    private readonly IActionDispatcher _actionDispatcher;
    private readonly IProfileManager _profileManager;
    private readonly ProfileStorageService _profileStorageService;
    private readonly GesturePipelineOrchestrator _pipeline;

    private readonly DispatcherTimer _foregroundCheckTimer;
    private FloatingOverlayWindow? _overlayWindow;
    private WriteableBitmap? _writeableBitmap;
    private HandPose? _latestPose;
    private bool _isRunning;
    private long _frameCounter = 0;

    // Connections between the 21 MediaPipe hand landmarks (skeletal bones)
    private static readonly (int From, int To)[] HandBones = new[]
    {
        (0, 1), (1, 2), (2, 3), (3, 4),        // Thumb
        (0, 5), (5, 6), (6, 7), (7, 8),        // Index
        (5, 9), (9, 10), (10, 11), (11, 12),   // Middle
        (9, 13), (13, 14), (14, 15), (15, 16), // Ring
        (13, 17), (17, 18), (18, 19), (19, 20),// Pinky
        (0, 17)                                // Palm base closure
    };

    public MainWindow()
    {
        InitializeComponent();

        // 1. Instantiate core domain, pipeline and persistence components
        _cameraService = new OpenCvCameraService();
        _handTracker = new OnnxHandTracker();
        _temporalFilter = new EmaTemporalFilter(alpha: 0.65f);
        _featureExtractor = new HandFeatureExtractor();
        _classifier = new StaticGestureClassifier();
        _gestureEngine = new GestureEngine();
        _actionDispatcher = new ActionDispatcher();
        _profileManager = new ProfileManager();
        _profileStorageService = new ProfileStorageService();

        _pipeline = new GesturePipelineOrchestrator(
            _cameraService,
            _handTracker,
            _temporalFilter,
            _featureExtractor,
            _classifier,
            _gestureEngine,
            _actionDispatcher,
            _profileManager);

        // 2. Wire pipeline events
        _cameraService.FrameCaptured += OnCameraFrameCaptured;
        _pipeline.StatisticsUpdated += OnStatisticsUpdated;
        _pipeline.HandPoseDetected += OnHandPoseDetected;
        _pipeline.GestureTriggered += OnGestureTriggered;

        // 3. Populate profiles in UI
        RefreshProfilesUi();
        _profileManager.ProfileChanged += OnProfileChanged;

        // 4. UIPI and Foreground window monitor timer
        _foregroundCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _foregroundCheckTimer.Tick += OnCheckForegroundWindow;
        _foregroundCheckTimer.Start();

        TxtTrackerEngine.Text = _handTracker.IsModelLoaded ? "ONNX Tensor Core" : "Modo Simulado / Test";

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CheckUipiStatus();

        // Ensure default profiles are generated on disk
        await _profileStorageService.EnsureDefaultProfilesAsync();
        var savedProfiles = await _profileStorageService.LoadAllProfilesAsync();
        if (savedProfiles.Count > 0)
        {
            _profileManager.LoadProfiles(savedProfiles);
            RefreshProfilesUi();
        }

        // Initialize and show transparent click-through floating overlay
        _overlayWindow = new FloatingOverlayWindow();
        if (ChkFloatingHud.IsChecked == true)
        {
            _overlayWindow.Show();
        }

        AppendLogEntry("🚀 MVPGesture Studio iniciado. Pipeline listo.");
        AppendLogEntry($"📁 Carpeta de perfiles: {_profileStorageService.ProfilesDirectory}");
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _foregroundCheckTimer.Stop();
        _ = _pipeline.StopAsync();
        _overlayWindow?.Close();
        _overlayWindow = null;
    }

    private void RefreshProfilesUi()
    {
        CmbProfiles.Items.Clear();
        foreach (var profile in _profileManager.AvailableProfiles)
        {
            CmbProfiles.Items.Add(profile.Name);
        }
        CmbProfiles.SelectedIndex = 0;
    }

    private async void BtnStartStop_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            _isRunning = true;
            BtnStartStop.Content = "⏹ Detener Pipeline";
            BtnStartStop.Background = new SolidColorBrush(Color.FromRgb(191, 97, 106)); // Nord red
            TxtStatusMessage.Text = "Pipeline activo y procesando fotogramas en paralelo...";
            AppendLogEntry("▶ Pipeline iniciado: Captura + ONNX + 10 Gestos.");

            await _pipeline.StartAsync();
        }
        else
        {
            _isRunning = false;
            BtnStartStop.Content = "▶ Iniciar Pipeline";
            BtnStartStop.Background = new SolidColorBrush(Color.FromRgb(94, 129, 172)); // Nord blue
            TxtStatusMessage.Text = "Pipeline detenido.";
            AppendLogEntry("⏹ Pipeline detenido.");

            await _pipeline.StopAsync();
            CanvasSkeleton.Children.Clear();
            _latestPose = null;
        }
    }

    private void OnCameraFrameCaptured(object? sender, (ReadOnlyMemory<byte> Buffer, CameraFrameMetadata Metadata) e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            RenderFrameBuffer(e.Buffer, e.Metadata.Width, e.Metadata.Height);

            if (ChkShowOverlay.IsChecked == true && _latestPose != null)
            {
                DrawSkeleton(_latestPose, CanvasSkeleton.ActualWidth, CanvasSkeleton.ActualHeight);
            }
            else
            {
                CanvasSkeleton.Children.Clear();
            }
        });
    }

    private void OnStatisticsUpdated(object? sender, PipelineStatistics stats)
    {
        _frameCounter++;
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            TxtFps.Text = $"{stats.Fps:F1} FPS";
            TxtLatency.Text = $"{stats.LatencyMs:F1} ms";

            if (stats.LatencyMs <= 80.0f)
            {
                TxtLatency.Foreground = new SolidColorBrush(Color.FromRgb(163, 190, 140)); // Green SLA (<80ms)
            }
            else
            {
                TxtLatency.Foreground = new SolidColorBrush(Color.FromRgb(235, 203, 139)); // Yellow warning
            }

            // Update Prominent Gesture Label & Icon
            if (stats.CurrentGesture != HandGestureType.None)
            {
                string tag = stats.CurrentGesture.IsDynamic() ? "[Dinámico] " : "";
                TxtGesture.Text = $"{tag}{stats.CurrentGesture}";
                TxtGesture.Foreground = stats.CurrentGesture.IsDynamic()
                    ? new SolidColorBrush(Color.FromRgb(235, 203, 139)) // Gold for swipes
                    : new SolidColorBrush(Color.FromRgb(136, 192, 208)); // Cyan for static
                TxtGestureIcon.Text = GetGestureEmoji(stats.CurrentGesture);
                TxtConfidenceBadge.Text = $"{stats.Confidence * 100:F0}%";
            }
            else
            {
                TxtGesture.Text = "Ninguno (Buscando mano)";
                TxtGesture.Foreground = new SolidColorBrush(Color.FromRgb(216, 222, 233));
                TxtGestureIcon.Text = "✋";
                TxtConfidenceBadge.Text = "0%";
            }

            TxtEngineState.Text = $"Engine: {(stats.IsActive ? "ARMADO / ACTIVO" : "PAUSADO (OpenHand para armar)")}";

            // Update Floating Click-Through HUD
            _overlayWindow?.UpdateState(
                stats.CurrentGesture,
                stats.Confidence,
                _profileManager.ActiveProfile.Name,
                stats.IsActive,
                null,
                false);

            // Verbose per-frame log (if enabled)
            if (ChkVerboseLog.IsChecked == true && stats.CurrentGesture != HandGestureType.None)
            {
                string frameLog = $"#{_frameCounter:D5} | Gesto: {stats.CurrentGesture,-12} | Conf: {stats.Confidence * 100:F0}% | Lat: {stats.LatencyMs:F1}ms";
                AppendLogEntry(frameLog);
                Trace.WriteLine(frameLog);
            }
        });
    }

    private void OnHandPoseDetected(object? sender, HandPose pose)
    {
        _latestPose = pose;
    }

    private void OnGestureTriggered(object? sender, GestureEvent e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            string desc = e.SuggestedAction?.Description ?? e.Gesture.ToString();
            TxtStatusMessage.Text = $"Acción confirmada: {desc} ({e.Gesture})";

            string triggerLog = $"⭐ [{DateTime.Now:HH:mm:ss.fff}] DISPARO: {e.Gesture} ➔ {desc} (Conf: {e.Confidence * 100:F0}%)";
            AppendLogEntry(triggerLog);
            Trace.WriteLine(triggerLog);
            Console.WriteLine(triggerLog);

            // Pulse overlay HUD with trigger feedback
            _overlayWindow?.UpdateState(
                e.Gesture,
                e.Confidence,
                _profileManager.ActiveProfile.Name,
                _gestureEngine.IsActive,
                e.SuggestedAction,
                true);
        });
    }

    private void AppendLogEntry(string message)
    {
        if (LstConsoleLog.Items.Count > 300)
        {
            LstConsoleLog.Items.RemoveAt(0);
        }

        string time = DateTime.Now.ToString("HH:mm:ss.fff");
        LstConsoleLog.Items.Add($"[{time}] {message}");

        if (ChkAutoScroll.IsChecked == true)
        {
            LstConsoleLog.ScrollIntoView(LstConsoleLog.Items[^1]);
        }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        LstConsoleLog.Items.Clear();
    }

    private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var item in LstConsoleLog.Items)
        {
            sb.AppendLine(item.ToString());
        }
        if (sb.Length > 0)
        {
            Clipboard.SetText(sb.ToString());
            TxtStatusMessage.Text = "Log copiado al portapapeles.";
        }
    }

    private static string GetGestureEmoji(HandGestureType gesture) => gesture switch
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

    private void RenderFrameBuffer(ReadOnlyMemory<byte> buffer, int width, int height)
    {
        if (width <= 0 || height <= 0 || buffer.IsEmpty) return;

        if (_writeableBitmap == null || _writeableBitmap.PixelWidth != width || _writeableBitmap.PixelHeight != height)
        {
            _writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
            ImgVideoPreview.Source = _writeableBitmap;
        }

        _writeableBitmap.Lock();
        try
        {
            var span = buffer.Span;
            unsafe
            {
                fixed (byte* pSrc = span)
                {
                    Buffer.MemoryCopy(pSrc, (void*)_writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride * height, span.Length);
                }
            }
            _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            _writeableBitmap.Unlock();
        }
    }

    private void DrawSkeleton(HandPose pose, double canvasWidth, double canvasHeight)
    {
        CanvasSkeleton.Children.Clear();
        if (canvasWidth <= 0) canvasWidth = 640;
        if (canvasHeight <= 0) canvasHeight = 380;

        var landmarks = pose.Landmarks;

        // Draw bone lines
        var lineBrush = new SolidColorBrush(Color.FromArgb(200, 136, 192, 208));
        foreach (var (from, to) in HandBones)
        {
            if (from >= landmarks.Count || to >= landmarks.Count) continue;

            var ptA = landmarks[from];
            var ptB = landmarks[to];

            var line = new Line
            {
                X1 = ptA.X * canvasWidth,
                Y1 = ptA.Y * canvasHeight,
                X2 = ptB.X * canvasWidth,
                Y2 = ptB.Y * canvasHeight,
                Stroke = lineBrush,
                StrokeThickness = 2.5
            };
            CanvasSkeleton.Children.Add(line);
        }

        // Draw joint circles
        var jointBrush = new SolidColorBrush(Color.FromRgb(163, 190, 140));
        var tipBrush = new SolidColorBrush(Color.FromRgb(235, 203, 139));

        for (int i = 0; i < landmarks.Count; i++)
        {
            var lm = landmarks[i];
            bool isTip = i is 4 or 8 or 12 or 16 or 20;

            var ellipse = new Ellipse
            {
                Width = isTip ? 8 : 5,
                Height = isTip ? 8 : 5,
                Fill = isTip ? tipBrush : jointBrush
            };

            Canvas.SetLeft(ellipse, (lm.X * canvasWidth) - (ellipse.Width / 2));
            Canvas.SetTop(ellipse, (lm.Y * canvasHeight) - (ellipse.Height / 2));
            CanvasSkeleton.Children.Add(ellipse);
        }
    }

    private void OnCheckForegroundWindow(object? sender, EventArgs e)
    {
        CheckUipiStatus();

        if (ChkAutoSwitch.IsChecked == true)
        {
            var title = Win32Input.GetActiveWindowTitle();
            var process = Win32Input.GetActiveProcessName();
            _profileManager.UpdateActiveWindow(title, process);
        }
    }

    private void CheckUipiStatus()
    {
        bool isForeElevated = UipiHelper.IsForegroundWindowElevated(out string procName);
        bool isSelfElevated = UipiHelper.IsCurrentProcessElevated();

        if (isForeElevated && !isSelfElevated)
        {
            TxtUipiStatus.Text = $"⚠️ Conflicto UIPI: '{procName}' se ejecuta como Administrador. Ejecute MVPGesture con privilegios elevados para interactuar con esta ventana.";
            TxtUipiStatus.Foreground = new SolidColorBrush(Color.FromRgb(235, 203, 139));
        }
        else
        {
            TxtUipiStatus.Text = isSelfElevated 
                ? "🛡️ UIPI: MVPGesture ejecutándose como Administrador (Acceso total)." 
                : "🛡️ UIPI: Permisos normales. Sin conflictos de privilegios.";
            TxtUipiStatus.Foreground = new SolidColorBrush(Color.FromRgb(163, 190, 140));
        }
    }

    private void OnProfileChanged(object? sender, AppProfile profile)
    {
        for (int i = 0; i < CmbProfiles.Items.Count; i++)
        {
            if (CmbProfiles.Items[i]?.ToString() == profile.Name)
            {
                CmbProfiles.SelectedIndex = i;
                break;
            }
        }
        TxtStatusMessage.Text = $"Perfil activo: {profile.Name} ({profile.Description})";
        AppendLogEntry($"🔄 Perfil conmutado a: {profile.Name}");
    }

    private void CmbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProfiles.SelectedItem != null)
        {
            var selectedName = CmbProfiles.SelectedItem.ToString();
            var profile = _profileManager.AvailableProfiles.FirstOrDefault(p => p.Name == selectedName);
            if (profile != null && _profileManager.ActiveProfile.Id != profile.Id)
            {
                _profileManager.SetActiveProfile(profile.Id);
            }
        }
    }

    private void ChkAutoSwitch_Changed(object sender, RoutedEventArgs e) { }

    private void ChkFloatingHud_Changed(object sender, RoutedEventArgs e)
    {
        if (_overlayWindow == null) return;
        if (ChkFloatingHud.IsChecked == true)
        {
            _overlayWindow.Show();
        }
        else
        {
            _overlayWindow.Hide();
        }
    }

    private async void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var active = _profileManager.ActiveProfile;
        if (active != null)
        {
            active.DeadZoneRadius = (float)SldDeadZone.Value;
            active.MouseSpeedMultiplier = (float)SldSensitivity.Value;
            active.SmoothingFactor = (float)SldSmoothing.Value;
            active.MinimumHoldDurationMs = (int)SldHold.Value;
            active.MinSwipeDistance = (float)SldSwipeDist.Value;
            active.MinSwipeVelocity = (float)SldSwipeVel.Value;

            await _profileStorageService.SaveProfileAsync(active);
            TxtStatusMessage.Text = $"Perfil '{active.Name}' guardado exitosamente en JSON.";
            AppendLogEntry($"💾 Guardado perfil '{active.Name}' ({active.Id}.json)");
        }
    }

    private async void BtnReloadProfiles_Click(object sender, RoutedEventArgs e)
    {
        var loaded = await _profileStorageService.LoadAllProfilesAsync();
        if (loaded.Count > 0)
        {
            _profileManager.LoadProfiles(loaded);
            RefreshProfilesUi();
            TxtStatusMessage.Text = $"{loaded.Count} perfiles recargados desde disco.";
            AppendLogEntry($"🔄 {loaded.Count} perfiles JSON recargados desde disco.");
        }
    }

    private void SldDeadZone_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtDeadZoneVal != null)
            TxtDeadZoneVal.Text = $"{e.NewValue:F3}";
        if (_gestureEngine != null)
            _gestureEngine.DeadZoneRadiusOverride = (float)e.NewValue;
    }

    private void SldSensitivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSensitivityVal != null)
            TxtSensitivityVal.Text = $"{e.NewValue:F1}x";
    }

    private void SldSmoothing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSmoothingVal != null)
            TxtSmoothingVal.Text = $"{e.NewValue:F2}";
        if (_temporalFilter != null)
            _temporalFilter.Alpha = (float)e.NewValue;
    }

    private void SldHold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtHoldVal != null)
            TxtHoldVal.Text = $"{e.NewValue:F0} ms";
        if (_gestureEngine != null)
            _gestureEngine.HoldDurationMsOverride = (int)e.NewValue;
    }

    private void SldSwipeDist_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSwipeDistVal != null)
            TxtSwipeDistVal.Text = $"{e.NewValue:F2}";
        if (_gestureEngine != null)
            _gestureEngine.MinSwipeDistanceOverride = (float)e.NewValue;
    }

    private void SldSwipeVel_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtSwipeVelVal != null)
            TxtSwipeVelVal.Text = $"{e.NewValue:F2} u/s";
        if (_gestureEngine != null)
            _gestureEngine.MinSwipeVelocityOverride = (float)e.NewValue;
    }
}
