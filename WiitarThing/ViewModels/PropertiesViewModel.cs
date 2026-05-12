namespace WiinUSoft.ViewModels;

public sealed class PropertiesViewModel : ViewModelBase
{
    private string _name;
    private string _profilePath;
    private int _autoConnectIndex;
    private int _rumbleIndex;
    private int _calibrationIndex;
    private int _pointerModeIndex;

    public PropertiesViewModel(Property source, string defaultName)
    {
        Source = source;
        _name = string.IsNullOrWhiteSpace(source.name) ? defaultName : source.name;
        _profilePath = source.profile;
        _autoConnectIndex = source.autoNum;
        _rumbleIndex = source.rumbleIntensity;
        _calibrationIndex = source.calPref switch
        {
            Property.CalibrationPreference.Minimal => 1,
            Property.CalibrationPreference.More => 2,
            Property.CalibrationPreference.Extra => 3,
            Property.CalibrationPreference.Custom => 4,
            _ => 0
        };
        _pointerModeIndex = (int)source.pointerMode;
    }

    public Property Source { get; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ProfilePath
    {
        get => _profilePath;
        set => SetProperty(ref _profilePath, value);
    }

    public int AutoConnectIndex
    {
        get => _autoConnectIndex;
        set => SetProperty(ref _autoConnectIndex, value);
    }

    public int RumbleIndex
    {
        get => _rumbleIndex;
        set => SetProperty(ref _rumbleIndex, value);
    }

    public int CalibrationIndex
    {
        get => _calibrationIndex;
        set => SetProperty(ref _calibrationIndex, value);
    }

    public int PointerModeIndex
    {
        get => _pointerModeIndex;
        set => SetProperty(ref _pointerModeIndex, value);
    }
}
