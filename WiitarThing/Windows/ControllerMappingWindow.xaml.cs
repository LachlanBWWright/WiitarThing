using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NintrollerLib;

namespace WiinUSoft
{
    public partial class ControllerMappingWindow : ContentDialog
    {
        public bool result = false;
        public Dictionary<string, string> map;

        private readonly ControllerType _deviceType;
        private readonly Dictionary<string, TextBox> _valueEditors = new Dictionary<string, TextBox>();
        private bool _loaded;

        private ControllerMappingWindow()
        {
            InitializeComponent();
            _deviceType = ControllerType.Wiimote;
            map = new Dictionary<string, string>();
            Loaded += OnLoaded;
        }

        public ControllerMappingWindow(Dictionary<string, string> mappings, ControllerType type)
        {
            InitializeComponent();
            _deviceType = type;
            map = mappings.ToDictionary(entry => entry.Key, entry => entry.Value);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            if (_loaded)
                return;

            _loaded = true;
            headerText.Text = $"Edit mappings for {_deviceType}. Keys on the left map to Xbox targets on the right.";
            BuildRows();
        }

        private void BuildRows()
        {
            rowsHost.Children.Clear();
            _valueEditors.Clear();

            foreach (var entry in map.OrderBy(k => k.Key))
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

                var key = new TextBlock
                {
                    Text = entry.Key,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.WrapWholeWords
                };
                Grid.SetColumn(key, 0);

                var value = new TextBox
                {
                    Text = entry.Value ?? string.Empty
                };
                Grid.SetColumn(value, 1);

                row.Children.Add(key);
                row.Children.Add(value);

                rowsHost.Children.Add(row);
                _valueEditors[entry.Key] = value;
            }
        }

        private void CollectMappings()
        {
            foreach (var key in _valueEditors.Keys.ToList())
            {
                map[key] = _valueEditors[key].Text?.Trim() ?? string.Empty;
            }
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            CollectMappings();
            result = true;
        }

        private void btnDefault_Click(object sender, RoutedEventArgs e)
        {
            foreach (var key in _valueEditors.Keys)
            {
                _valueEditors[key].Text = key;
            }
            CollectMappings();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            CollectMappings();
            result = true;
            Hide();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            result = false;
            Hide();
        }
    }
}
