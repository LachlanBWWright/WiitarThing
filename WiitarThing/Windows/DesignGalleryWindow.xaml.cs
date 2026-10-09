using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using NintrollerLib;
using WiinUSoft.ViewModels;

namespace WiinUSoft.Windows;

internal sealed partial class DesignGalleryWindow : Window
{
    private readonly string[] _stateNames =
    {
        "empty",
        "detected-devices",
        "connected-controllers",
        "driver-warning",
        "sync-in-progress",
        "disconnected-controller"
    };
    private readonly GalleryDeviceFixture _detected;
    private readonly GalleryDeviceFixture _connected;
    private readonly GalleryDeviceFixture _failed;
    private readonly GalleryDeviceFixture _disconnected;

    public DesignGalleryWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        _detected = new("gallery:detected", "Discovered Wii Guitar", ControllerType.Guitar,
            DeviceState.Discovered, VirtualOutputMode.VJoyExperimental);
        _connected = new("gallery:connected", "Player 1 Wii Guitar", ControllerType.Guitar, DeviceState.Connected_XInput);
        _connected.ViewModel.GuitarPreviewModeIndex = (int)GuitarPreviewMode.VirtualOutput;
        _failed = new("gallery:failed", "Wii Guitar - output failed", ControllerType.Guitar, DeviceState.Connected_XInput);
        // Raise the real view-model failure event before attaching a view that prompts with a dialog.
        _failed.Session.FailReconnect = true;
        _failed.ViewModel.VirtualOutputModeIndex = (int)VirtualOutputMode.VJoyExperimental;
        _disconnected = new("gallery:disconnected", "Wii Guitar - Bluetooth disconnected", ControllerType.Guitar, DeviceState.Connected_XInput);
        AddFixture(_detected, DetectedRows);
        AddFixture(_connected, ConnectedRows);
        AddFixture(_failed, WarningRows);
        AddFixture(_disconnected, DisconnectedRows);
        _disconnected.Session.LoseConnection();
        Closed += (_, _) =>
        {
            _detected.Dispose();
            _connected.Dispose();
            _failed.Dispose();
            _disconnected.Dispose();
        };
        stateSelector.SelectedIndex = 0;
    }

    private static void AddFixture(GalleryDeviceFixture fixture, StackPanel rows)
    {
        fixture.CreateControl();
        rows.Children.Add(fixture.Control);
    }

    private void StateSelector_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (stateSelector.SelectedIndex >= 0)
            ShowState(stateSelector.SelectedIndex);
    }

    private void ShowState(int stateIndex)
    {
        EmptyState.Visibility = Visibility.Collapsed;
        DetectedState.Visibility = Visibility.Collapsed;
        ConnectedState.Visibility = Visibility.Collapsed;
        WarningState.Visibility = Visibility.Collapsed;
        SyncState.Visibility = Visibility.Collapsed;
        DisconnectedState.Visibility = Visibility.Collapsed;

        switch (stateIndex)
        {
            case 0: EmptyState.Visibility = Visibility.Visible; break;
            case 1: DetectedState.Visibility = Visibility.Visible; break;
            case 2: ConnectedState.Visibility = Visibility.Visible; break;
            case 3: WarningState.Visibility = Visibility.Visible; break;
            case 4: SyncState.Visibility = Visibility.Visible; break;
            case 5: DisconnectedState.Visibility = Visibility.Visible; break;
        }
    }

    private async void CaptureCurrent_Click(object sender, RoutedEventArgs e)
    {
        await CaptureStateAsync(stateSelector.SelectedIndex);
    }

    private async void CaptureAll_Click(object sender, RoutedEventArgs e)
    {
        await CaptureAllAsync();
    }

    internal async Task CaptureAllAsync()
    {
        stateSelector.IsEnabled = false;
        captureStatus.Text = "Capturing gallery states...";
        try
        {
            await ValidateDeviceBindingsAsync();
            for (int i = 0; i < _stateNames.Length; i++)
            {
                stateSelector.SelectedIndex = i;
                await Task.Delay(120);
                await CaptureStateAsync(i);
            }

            captureStatus.Text = "Captured all states to artifacts/ui-gallery";
        }
        finally
        {
            stateSelector.IsEnabled = true;
        }
    }

    private async Task ValidateDeviceBindingsAsync()
    {
        _detected.ViewModel.VJoyDeviceId = 2;
        _connected.ViewModel.VirtualOutputModeIndex = (int)VirtualOutputMode.ScpXbox360;
        _connected.ViewModel.VJoyDeviceId = 2;
        int initialReconnects = _connected.Session.Reconnects;
        ShowState(1);
        await Task.Delay(120);
        var idEditor = FindElement<NumberBox>(_detected.Control);
        Require(idEditor.Visibility == Visibility.Visible && idEditor.Value == 2, "Discovered guitar vJoy settings are missing.");
        idEditor.Value = 3;
        await Task.Delay(80);
        Require(_detected.ViewModel.VJoyDeviceId == 3 && _detected.Configuration.Saves > 0, "vJoy ID editor did not save through its two-way binding.");
        Require(FindNamed<Button>(_detected.Control, "btnXinput").Visibility == Visibility.Visible, "Discovered guitar has no Connect action.");

        ShowState(2);
        await Task.Delay(120);
        var outputSelector = FindNamed<ComboBox>(_connected.Control, "outputModeSelector");
        outputSelector.SelectedIndex = (int)VirtualOutputMode.VJoyExperimental;
        await Task.Delay(80);
        Require(_connected.Session.Reconnects == initialReconnects + 1, "Changing connected output mode did not reconnect.");
        var connectedEditor = FindElement<NumberBox>(_connected.Control);
        connectedEditor.Value = 4;
        await Task.Delay(80);
        Require(_connected.ViewModel.VJoyDeviceId == 4 && _connected.Session.Reconnects == initialReconnects + 2, "Changing connected vJoy ID did not reconnect.");
        Require(FindNamed<Button>(_connected.Control, "btnDetatch").Visibility == Visibility.Visible, "Connected guitar has no Disconnect action.");
        Require(FindNamed<StackPanel>(_connected.Control, "guitarPreview").Visibility == Visibility.Visible, "Connected guitar preview is hidden.");
        Require(FindNamed<TextBlock>(_connected.Control, "virtualOutputStatusText").Text == "Virtual output: connected",
            "Connected guitar output status is stale.");
        _connected.ViewModel.DisconnectCommand.Execute(null);
        await Task.Delay(80);
        Require(!_connected.ViewModel.IsConnected && FindNamed<Button>(_connected.Control, "btnXinput").Visibility == Visibility.Visible,
            "Disconnect command did not return the row to its discovered state.");
        _connected.ViewModel.ConnectToXInput(1);
        await Task.Delay(80);
        Require(_connected.ViewModel.IsConnected && FindNamed<Button>(_connected.Control, "btnDetatch").Visibility == Visibility.Visible,
            "Reconnecting did not restore the connected row.");

        ShowState(3);
        await Task.Delay(120);
        Require(!_failed.ViewModel.IsConnected && _failed.Session.Reconnects == 1 && !string.IsNullOrEmpty(_failed.ViewModel.OutputError), "Failed reconnect did not retain its error on the disconnected row.");
        Require(ContainsVisibleText(_failed.Control, _failed.ViewModel.OutputError), "Connection error binding is hidden or empty.");
        var errorText = FindNamed<TextBlock>(_failed.Control, "outputErrorText");
        var outputPanel = FindNamed<StackPanel>(_failed.Control, "virtualOutputPanel");
        var errorRight = errorText.TransformToVisual(_failed.Control).TransformPoint(new global::Windows.Foundation.Point(errorText.ActualWidth, 0));
        var outputLeft = outputPanel.TransformToVisual(_failed.Control).TransformPoint(new global::Windows.Foundation.Point(0, 0));
        Require(errorRight.X <= outputLeft.X, "Connection error text overlaps the virtual output settings.");
        _failed.Session.Refresh();
        await Task.Delay(80);
        Require(ContainsVisibleText(_failed.Control, _failed.ViewModel.OutputError), "Refreshing the failed row removed its error.");

        ShowState(5);
        await Task.Delay(120);
        DisconnectedRows.Children.Remove(_disconnected.Control);
        await Task.Delay(80);
        DisconnectedRows.Children.Add(_disconnected.Control);
        _disconnected.Session.Refresh();
        await Task.Delay(80);
        Require(!FindNamed<Button>(_disconnected.Control, "btnIdentify").IsEnabled &&
            FindNamed<TextBlock>(_disconnected.Control, "labelUnavailable").Visibility == Visibility.Visible &&
            FindNamed<Button>(_disconnected.Control, "btnDetatch").Visibility == Visibility.Collapsed &&
            FindNamed<StackPanel>(_disconnected.Control, "virtualOutputPanel").Visibility == Visibility.Collapsed,
            "Disconnected guitar retained active actions or output settings.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"UI gallery validation: {message}");
    }

    private static T FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement
        => FindElement<T>(root, element => element.Name == name);

    private static T FindElement<T>(DependencyObject root, Func<T, bool>? predicate = null) where T : FrameworkElement
    {
        if (root is T match && (predicate is null || predicate(match))) return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = TryFindElement(VisualTreeHelper.GetChild(root, i), predicate);
            if (child is not null) return child;
        }
        throw new InvalidOperationException($"UI gallery validation: missing {typeof(T).Name}.");
    }

    private static T? TryFindElement<T>(DependencyObject root, Func<T, bool>? predicate) where T : FrameworkElement
    {
        if (root is T match && (predicate is null || predicate(match))) return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = TryFindElement(VisualTreeHelper.GetChild(root, i), predicate);
            if (child is not null) return child;
        }
        return null;
    }

    private static bool ContainsVisibleText(DependencyObject root, string text)
        => TryFindElement<TextBlock>(root, block => block.Text == text && block.Visibility == Visibility.Visible) is not null;

    private async Task CaptureStateAsync(int stateIndex)
    {
        if (stateIndex < 0 || stateIndex >= _stateNames.Length)
            return;

        ShowState(stateIndex);
        await Task.Delay(80);

        string outputDirectory = FindOutputDirectory();
        Directory.CreateDirectory(outputDirectory);
        string path = Path.Combine(outputDirectory, $"{_stateNames[stateIndex]}.png");

        var bitmap = new RenderTargetBitmap();
        await bitmap.RenderAsync(GallerySurface);
        var pixels = await bitmap.GetPixelsAsync();
        StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(outputDirectory);
        StorageFile file = await folder.CreateFileAsync(
            $"{_stateNames[stateIndex]}.png",
            CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth,
            (uint)bitmap.PixelHeight,
            96,
            96,
            pixels.ToArray());
        await encoder.FlushAsync();

        captureStatus.Text = $"Captured {_stateNames[stateIndex]}.png";
    }

    private static string FindOutputDirectory()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BUILD_AND_TEST.md")))
                return Path.Combine(current.FullName, "artifacts", "ui-gallery");

            current = current.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "ui-gallery");
    }
}
