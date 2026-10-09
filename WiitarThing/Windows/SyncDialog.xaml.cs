using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using WiinUSoft.Services;
using WiinUSoft.ViewModels;

namespace WiinUSoft.Windows;

/// <summary>Thin synchronization view: it owns only dialog lifecycle and cancellation UI.</summary>
public partial class SyncDialog : ContentDialog
{
    private readonly SyncViewModel _viewModel = new();
    private readonly BluetoothSyncWorkflow _workflow = new();
    private CancellationTokenSource? _cancellation;
    private Task? _scanTask;
    private bool _started;

    public SyncDialog()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    public event EventHandler? NewDeviceFound;

    private void ContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        if (_started) return;
        _started = true;
        _viewModel.Start();
        _cancellation = new CancellationTokenSource();
        _scanTask = Task.Run(() =>
        {
            _workflow.Run(_cancellation.Token, _viewModel.AppendOutput, DeviceFound);
            _viewModel.Stop(_viewModel.Count > 0, compatibleRadio: true);
        });
    }

    private void DeviceFound()
    {
        _viewModel.AddDevice();
        NewDeviceFound?.Invoke(this, EventArgs.Empty);
    }

    private async void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!_viewModel.IsRunning) return;
        args.Cancel = true;
        var deferral = args.GetDeferral();
        try
        {
            _viewModel.Cancel();
            _cancellation?.Cancel();
            if (_scanTask is not null) await _scanTask;
            Hide();
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            deferral.Complete();
        }
    }
}
