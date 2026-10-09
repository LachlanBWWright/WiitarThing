using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WiinUSoft.ViewModels;

public sealed class ControllerMappingViewModel : ViewModelBase
{
    public ControllerMappingViewModel(Dictionary<string, string> map)
    {
        Rows = new ObservableCollection<MappingRowViewModel>(
            map.OrderBy(entry => entry.Key)
               .Select(entry => new MappingRowViewModel(entry.Key, entry.Value ?? string.Empty)));
    }

    public ObservableCollection<MappingRowViewModel> Rows { get; }

    public string HeaderText { get; set; } = "Edit mappings for the selected controller. Keys on the left map to Xbox targets on the right.";

    public Dictionary<string, string> ToDictionary()
    {
        return Rows.ToDictionary(row => row.Source, row => row.Target?.Trim() ?? string.Empty);
    }

    public void ResetToDefault()
    {
        foreach (var row in Rows)
            row.Target = row.Source;
    }
}

public sealed class MappingRowViewModel : ViewModelBase
{
    private string _target;

    public MappingRowViewModel(string source, string target)
    {
        Source = source;
        _target = target;
    }

    public string Source { get; }

    public string Target
    {
        get => _target;
        set => SetProperty(ref _target, value);
    }
}
