using System.Text;

namespace WiinUSoft.ViewModels;

public sealed class SyncViewModel : ViewModelBase
{
    private readonly StringBuilder _output = new();
    private bool _isRunning;
    private bool _isCancelled;
    private int _count;
    private string _closeButtonText = "Cancel Scan";

    public bool IsRunning
    {
        get => _isRunning;
        private set => SetProperty(ref _isRunning, value);
    }

    public bool IsCancelled
    {
        get => _isCancelled;
        private set => SetProperty(ref _isCancelled, value);
    }

    public int Count
    {
        get => _count;
        private set => SetProperty(ref _count, value);
    }

    public string Output => _output.ToString();

    public string CloseButtonText
    {
        get => _closeButtonText;
        private set => SetProperty(ref _closeButtonText, value);
    }

    public void Start()
    {
        IsRunning = true;
        IsCancelled = false;
        Count = 0;
        CloseButtonText = "Cancel Scan";
        _output.Clear();
        OnPropertyChanged(nameof(Output));
    }

    public void Cancel()
    {
        IsCancelled = true;
    }

    public void AddDevice()
    {
        Count++;
    }

    public void AppendOutput(string text)
    {
        _output.AppendLine(text);
        OnPropertyChanged(nameof(Output));
    }

    public void Stop(bool foundDevice, bool compatibleRadio)
    {
        IsRunning = false;
        if (Count > 0 || !compatibleRadio)
            CloseButtonText = "OK";
    }
}
