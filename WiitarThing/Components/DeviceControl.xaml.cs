using System;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NintrollerLib;
using Shared;
using WiinUSoft.Holders;
using WiinUSoft.Models;
using WiinUSoft.VirtualOutput;
using WiinUSoft.ViewModels;

namespace WiinUSoft;

/// <summary>Visual representation of a device. Hardware lifetime and state are owned by the session.</summary>
public partial class DeviceControl : UserControl
{
    public static readonly DependencyProperty SessionProperty =
        DependencyProperty.Register(nameof(Session), typeof(IDeviceSession), typeof(DeviceControl), new PropertyMetadata(null, OnSessionChanged));

    private static readonly SolidColorBrush PreviewInactiveBrush = new(Colors.Transparent);
    private static readonly SolidColorBrush PreviewTextInactiveBrush = new(Colors.Gray);
    private static readonly SolidColorBrush PreviewTextActiveBrush = new(Colors.White);
    private static readonly SolidColorBrush FretGBrush = new(global::Windows.UI.Color.FromArgb(255, 45, 164, 78));
    private static readonly SolidColorBrush FretRBrush = new(global::Windows.UI.Color.FromArgb(255, 209, 52, 56));
    private static readonly SolidColorBrush FretYBrush = new(global::Windows.UI.Color.FromArgb(255, 255, 200, 61));
    private static readonly SolidColorBrush FretBBrush = new(global::Windows.UI.Color.FromArgb(255, 0, 120, 212));
    private static readonly SolidColorBrush FretOBrush = new(global::Windows.UI.Color.FromArgb(255, 255, 140, 0));

    private IDeviceSession? _session;
    private DeviceViewModel? _viewModel;
    private bool _editingName;

    public DeviceControl()
    {
        InitializeComponent();
        DataContextChanged += DeviceControlDataContextChanged;
        Loaded += (_, _) =>
        {
            if (_session is null && Session is IDeviceSession session)
                AttachSession(session);
            if (_viewModel is null && DataContext is DeviceViewModel viewModel)
            {
                _viewModel = viewModel;
                viewModel.PropertyChanged += ViewModelPropertyChanged;
            }
        };
        Unloaded += (_, _) =>
        {
            if (_session is not null)
                DetachSession(_session);
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= ViewModelPropertyChanged;
                _viewModel = null;
            }
        };
    }

    private void DeviceControlDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        _viewModel = args.NewValue as DeviceViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += ViewModelPropertyChanged;
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeviceViewModel.GuitarPreviewModeIndex))
            DispatcherQueue.TryEnqueue(UpdateGuitarPreviewPanel);
    }

    public IDeviceSession? Session
    {
        get => (IDeviceSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DeviceControl)d;
        if (e.OldValue is IDeviceSession oldSession) control.DetachSession(oldSession);
        if (e.NewValue is IDeviceSession newSession) control.AttachSession(newSession);
    }

    private void AttachSession(IDeviceSession session)
    {
        _session = session;
        session.StateChanged += SessionStateChanged;
        session.ConnectionLost += SessionConnectionLost;
        session.ConnectionFailed += SessionConnectionFailed;
        session.BatteryChanged += SessionBatteryChanged;
        session.PreviewChanged += SessionPreviewChanged;
        session.InlineOutputError += SessionInlineOutputError;
        RefreshVisuals();
    }

    private void DetachSession(IDeviceSession session)
    {
        session.StateChanged -= SessionStateChanged;
        session.ConnectionLost -= SessionConnectionLost;
        session.ConnectionFailed -= SessionConnectionFailed;
        session.BatteryChanged -= SessionBatteryChanged;
        session.PreviewChanged -= SessionPreviewChanged;
        session.InlineOutputError -= SessionInlineOutputError;
        if (ReferenceEquals(_session, session)) _session = null;
    }

    private void SessionStateChanged(object? sender, DeviceSessionStateChangedEventArgs e) => DispatcherQueue.TryEnqueue(RefreshVisuals);
    private void SessionConnectionLost(object? sender, DeviceSessionConnectionLostEventArgs e) => DispatcherQueue.TryEnqueue(RefreshVisuals);
    private void SessionBatteryChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() =>
        statusGradient.Background = new SolidColorBrush(_session?.IsBatteryLow == true ? Colors.OrangeRed : Colors.Transparent));
    private void SessionPreviewChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(UpdateGuitarPreviewPanel);
    private void SessionInlineOutputError(object? sender, string e) => DispatcherQueue.TryEnqueue(() =>
    {
        if (ReferenceEquals(sender, _session))
            virtualOutputStatusText.Text = string.IsNullOrEmpty(e) ? "Virtual output: connected" : $"Virtual output: {e}";
    });
    private void SessionConnectionFailed(object? sender, DeviceSessionConnectionFailedEventArgs e) => DispatcherQueue.TryEnqueue(async () =>
    {
        if (XamlRoot is not null)
        {
            var dialog = new ContentDialog
            {
                Title = "Virtual Output Failed",
                Content = e.Error.ToDisplayString(),
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await VirtualControllerDriverPrompt.ShowDialogAsync(dialog);
        }
        if (e.Error.Kind == VirtualControllerErrorKind.DriverNotReady)
            await VirtualControllerDriverPrompt.PromptInstallAsync(XamlRoot);
    });

    private void RefreshVisuals()
    {
        if (_session is null) return;
        UpdateIcon(_session.DeviceType);
        labelName.Text = _session.DisplayName;
        nameInput.Text = _session.DisplayName;
        autoConnectNumber.SelectedIndex = Math.Clamp(_session.AutoConnectSlot, 0, autoConnectNumber.Items.Count - 1);
        statusGradient.Background = new SolidColorBrush(_session.IsBatteryLow ? Colors.OrangeRed : Colors.Transparent);
        ApplyStateVisuals(_session.ConnectionState);
    }

    private void ApplyStateVisuals(DeviceState state)
    {
        bool available = state != DeviceState.None;
        bool connected = state == DeviceState.Connected_XInput;
        labelUnavailable.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
        btnIdentify.IsEnabled = available;
        btnEditName.IsEnabled = available;
        autoConnectNumber.IsEnabled = available;
        btnXinput.IsEnabled = available && !connected;
        btnDetatch.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        btnDetatch.IsEnabled = connected;
#if DEBUG
        btnDebugView.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
#else
        btnDebugView.Visibility = Visibility.Collapsed;
#endif
        bool guitar = available && _session?.DeviceType == ControllerType.Guitar;
        guitarPreview.Visibility = guitar && connected ? Visibility.Visible : Visibility.Collapsed;
        virtualOutputPanel.Visibility = guitar ? Visibility.Visible : Visibility.Collapsed;
        if (connected)
        {
            string? error = (DataContext as DeviceViewModel)?.OutputError;
            virtualOutputStatusText.Text = string.IsNullOrEmpty(error) ? "Virtual output: connected" : $"Virtual output: {error}";
        }
        if (guitar && connected) UpdateGuitarPreviewPanel();
        if (!connected)
        {
            virtualOutputStatusText.Text = "Virtual output: not connected";
            virtualSourceStateText.Text = "Source: n/a";
            virtualReadbackStateText.Text = "Readback: unavailable";
            virtualOutputDiffText.Text = "Mismatch: n/a";
        }
    }

    private void UpdateIcon(ControllerType type)
    {
        string key = type switch
        {
            ControllerType.ProController => "ProIcon",
            ControllerType.ClassicControllerPro => "CCPIcon",
            ControllerType.ClassicController => "CCIcon",
            ControllerType.Nunchuk or ControllerType.NunchukB => "WNIcon",
            ControllerType.Guitar => "WGTIcon",
            ControllerType.Drums => "WDRIcon",
            _ => "WIcon"
        };
        icon.Source = Application.Current.Resources[key] as ImageSource;
    }

    private void UpdateGuitarPreviewPanel()
    {
        if (_session is null) return;
        var state = _session.LastPreviewState;
        var output = GuitarPreviewState.Empty;
        if (DataContext is DeviceViewModel vm && vm.GuitarPreviewModeIndex == (int)GuitarPreviewMode.VirtualOutput)
        {
            if (_session.TryReadVirtualOutput(out var read, out _))
            {
                state = read;
                output = read;
                virtualReadbackStateText.Text = $"Readback: {read.ToCompactDebugString()}";
            }
            else virtualReadbackStateText.Text = "Readback: unavailable";
        }
        else virtualReadbackStateText.Text = "Readback: disabled";
        UpdateGuitarPreview(state);
        var source = _session.LastPreviewState;
        virtualSourceStateText.Text = $"Source: {source.ToCompactDebugString()}";
        virtualOutputDiffText.Text = BuildMismatchSummary(source, output);
    }

    private void UpdateGuitarPreview(GuitarPreviewState state)
    {
        SetFretPreview(fretG, state.Green, FretGBrush); SetFretPreview(fretR, state.Red, FretRBrush);
        SetFretPreview(fretY, state.Yellow, FretYBrush); SetFretPreview(fretB, state.Blue, FretBBrush);
        SetFretPreview(fretO, state.Orange, FretOBrush); strumIndicator.Text = GetStrumPreview(state);
        SetTextPreview(previewWH, state.Whammy > 0.05f); SetTextPreview(previewWL, state.Whammy < -0.05f);
        SetTextPreview(previewTH, state.Tilt > 0.05f); SetTextPreview(previewTL, state.Tilt < -0.05f);
        SetTextPreview(previewStart, state.Start); SetTextPreview(previewSelect, state.Select);
    }

    private static string BuildMismatchSummary(GuitarPreviewState source, GuitarPreviewState output)
    {
        if (output == GuitarPreviewState.Empty) return "Mismatch: n/a";
        var mismatch = new System.Collections.Generic.List<string>();
        if (source.Green != output.Green) mismatch.Add("G");
        if (source.Red != output.Red) mismatch.Add("R");
        if (source.Yellow != output.Yellow) mismatch.Add("Y");
        if (source.Blue != output.Blue) mismatch.Add("B");
        if (source.Orange != output.Orange) mismatch.Add("O");
        if (source.StrumUp != output.StrumUp) mismatch.Add("SU");
        if (source.StrumDown != output.StrumDown) mismatch.Add("SD");
        if (source.Start != output.Start) mismatch.Add("St");
        if (source.Select != output.Select) mismatch.Add("Sl");
        if (Math.Abs(source.Whammy - output.Whammy) > 0.12f) mismatch.Add("Wh");
        if (Math.Abs(source.Tilt - output.Tilt) > 0.12f) mismatch.Add("Tl");
        return mismatch.Count == 0 ? "Mismatch: none" : $"Mismatch: {string.Join(", ", mismatch)}";
    }

    private static void SetFretPreview(Border fret, bool pressed, SolidColorBrush brush) => fret.Background = pressed ? brush : PreviewInactiveBrush;
    private static void SetTextPreview(TextBlock text, bool active)
    {
        text.Foreground = active ? PreviewTextActiveBrush : PreviewTextInactiveBrush;
        text.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
    }

    #pragma warning disable CS0162
    private static string GetStrumPreview(GuitarPreviewState state)
    {
        bool asciiPreview = Environment.TickCount != int.MinValue;
        if (asciiPreview)
        {
            if (state.StrumUp && state.DPadRight) return "^>"; if (state.StrumUp && state.DPadLeft) return "^<";
            if (state.StrumDown && state.DPadRight) return "v>"; if (state.StrumDown && state.DPadLeft) return "v<";
            if (state.StrumUp) return "^"; if (state.StrumDown) return "v";
            if (state.DPadLeft) return "<"; if (state.DPadRight) return ">"; return "-";
        }
#if false
        if (state.StrumUp && state.DPadRight) return "↗"; if (state.StrumUp && state.DPadLeft) return "↖";
        if (state.StrumDown && state.DPadRight) return "↘"; if (state.StrumDown && state.DPadLeft) return "↙";
        if (state.StrumUp) return "↑"; if (state.StrumDown) return "↓";
        if (state.DPadLeft) return "←"; if (state.DPadRight) return "→"; return "•";
    }

 #endif
        if (state.StrumUp && state.DPadRight) return "↗"; if (state.StrumUp && state.DPadLeft) return "↖";
        if (state.StrumDown && state.DPadRight) return "↘"; if (state.StrumDown && state.DPadLeft) return "↙";
        if (state.StrumUp) return "↑"; if (state.StrumDown) return "↓";
        if (state.DPadLeft) return "←"; if (state.DPadRight) return "→"; return "•";
    }

    #pragma warning restore CS0162
    private void IconRightTapped(object sender, RightTappedRoutedEventArgs e) => FlyoutBase.ShowAttachedFlyout(icon);

    private void XInputFlyout_Opening(object sender, object e)
    {
        XOption1.IsEnabled = Holders.XInputHolder.availabe[0]; XOption2.IsEnabled = Holders.XInputHolder.availabe[1];
        XOption3.IsEnabled = Holders.XInputHolder.availabe[2]; XOption4.IsEnabled = Holders.XInputHolder.availabe[3];
    }

    private async void XOptionClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || DataContext is not DeviceViewModel vm) return;
        var item = (MenuFlyoutItem)sender;
        if (_session.DeviceType != ControllerType.ProController)
        {
            var dialog = new ContentDialog { Title = "Connect Wii Remote", Content = "Press 1+2 on the Wii remote and press OK to continue.", PrimaryButtonText = "OK", CloseButtonText = "Cancel", XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        }
        if (int.TryParse(item.Name.Replace("XOption", ""), out int player)) vm.ConnectToXInput(player);
    }

    private async void BtnIdentifyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DeviceViewModel vm) return;
        btnIdentify.IsEnabled = false;
        try { await vm.IdentifyAsync(); }
        catch (Exception error) { WiitarDebug.Log($"Identify failed: {error}", WiitarDebug.LogLevel.Error); }
        finally { RefreshVisuals(); }
    }

    private void BtnXinputIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) => btnXinput.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;

    private void BtnEditNameClick(object sender, RoutedEventArgs e)
    {
        _editingName = true; nameInput.Text = _session?.DisplayName ?? string.Empty; labelName.Visibility = Visibility.Collapsed;
        nameInput.Visibility = Visibility.Visible; nameInput.Focus(FocusState.Programmatic); nameInput.SelectAll();
    }

    private void NameInputTextChanged(object sender, TextChangedEventArgs e) { }
    private void NameInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Enter) { CommitNameEdit(); e.Handled = true; }
        else if (e.Key == global::Windows.System.VirtualKey.Escape) { EndNameEdit(); e.Handled = true; }
    }
    private void NameInputLostFocus(object sender, RoutedEventArgs e) { if (_editingName) CommitNameEdit(); }
    private void CommitNameEdit() { if (DataContext is DeviceViewModel vm) vm.DisplayName = nameInput.Text; EndNameEdit(); }
    private void EndNameEdit() { _editingName = false; nameInput.Visibility = Visibility.Collapsed; labelName.Visibility = Visibility.Visible; }

#if DEBUG
    private Windows.DebugDataWindow? _debugWindow;
    private void BtnDebugViewClick(object sender, RoutedEventArgs e)
    {
        if (_debugWindow is not null || _session is not IConnectableDeviceSession connectable) return;
        _debugWindow = new Windows.DebugDataWindow { nintroller = connectable.Controller, XamlRoot = XamlRoot };
        _debugWindow.RegisterNintrollerUpdate(); _debugWindow.Closed += (_, _) => _debugWindow = null; _ = _debugWindow.ShowAsync();
    }
#else
    private void BtnDebugViewClick(object sender, RoutedEventArgs e) { }
#endif
}
