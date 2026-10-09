using System.Collections.Generic;
using System.Collections.ObjectModel;
using Shared;
using WiinUSoft;
using WiinUSoft.Services;

namespace WiinUSoft.ViewModels;

public sealed class DefaultCalibrationViewModel : ViewModelBase
{
    private readonly IDefaultCalibrationService _service;

    public DefaultCalibrationViewModel(IDefaultCalibrationService? service = null)
        => _service = service ?? new DefaultCalibrationService();

    private bool _minimum;
    private bool _default = true;
    private bool _moderate;
    private bool _extra;
    private bool _copy;
    private int _copyIndex = -1;
    private List<Property> _copyPreferences = new();

    public bool Minimum { get => _minimum; set => SetProperty(ref _minimum, value); }
    public bool Default { get => _default; set => SetProperty(ref _default, value); }
    public bool Moderate { get => _moderate; set => SetProperty(ref _moderate, value); }
    public bool Extra { get => _extra; set => SetProperty(ref _extra, value); }
    public bool Copy { get => _copy; set => SetProperty(ref _copy, value); }
    public int CopyIndex { get => _copyIndex; set => SetProperty(ref _copyIndex, value); }

    public ObservableCollection<string> CopyOptions { get; } = new();
    public bool CanCopy => CopyOptions.Count > 0;

    public void Load(Property? defaultProperty, IEnumerable<Property> devicePreferences)
    {
        Minimum = defaultProperty?.calPref == Property.CalibrationPreference.Minimal;
        Default = defaultProperty is null || defaultProperty.calPref == Property.CalibrationPreference.Default;
        Moderate = defaultProperty?.calPref == Property.CalibrationPreference.More;
        Extra = defaultProperty?.calPref == Property.CalibrationPreference.Extra;

        _copyPreferences = new List<Property>();
        CopyOptions.Clear();
        foreach (var preference in devicePreferences)
        {
            if (preference.hid == "all")
                continue;

            _copyPreferences.Add(preference);
            CopyOptions.Add(preference.name);
        }

        CopyIndex = CopyOptions.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(CanCopy));
    }

    public Property CreateProperty()
    {
        var property = new Property { hid = "all", name = "Default" };
        if (Minimum)
            property.calPref = Property.CalibrationPreference.Minimal;
        else if (Default)
            property.calPref = Property.CalibrationPreference.Default;
        else if (Moderate)
            property.calPref = Property.CalibrationPreference.More;
        else if (Extra)
            property.calPref = Property.CalibrationPreference.Extra;
        else if (Copy && CopyIndex >= 0 && CopyIndex < _copyPreferences.Count)
        {
            var source = _copyPreferences[CopyIndex];
            property.calPref = Property.CalibrationPreference.Custom;
            property.autoConnect = source.autoConnect;
            property.autoNum = source.autoNum;
            property.calString = source.calString;
            property.rumbleIntensity = source.rumbleIntensity;
            property.useRumble = source.useRumble;
        }

        return property;
    }

    public Result<Unit, PreferencesError> Save() => _service.SaveDefault(CreateProperty());
    public Result<Unit, PreferencesError> Clear() => _service.ClearDefault();
    public void LoadCurrent() => Load(_service.DefaultProperty, _service.DevicePreferences);
}
