using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace WiinUSoft.Windows;

internal sealed partial class DesignGalleryWindow : Window
{
    private readonly string[] _stateNames =
    {
        "empty",
        "detected-devices",
        "connected-controllers",
        "driver-warning",
        "sync-in-progress"
    };

    public DesignGalleryWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        stateSelector.SelectedIndex = 0;
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

        switch (stateIndex)
        {
            case 0: EmptyState.Visibility = Visibility.Visible; break;
            case 1: DetectedState.Visibility = Visibility.Visible; break;
            case 2: ConnectedState.Visibility = Visibility.Visible; break;
            case 3: WarningState.Visibility = Visibility.Visible; break;
            case 4: SyncState.Visibility = Visibility.Visible; break;
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
