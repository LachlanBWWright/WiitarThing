using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NintrollerLib;
using WiinUSoft.ViewModels;

namespace WiinUSoft
{
    public partial class ControllerMappingWindow : ContentDialog
    {
        public bool result = false;
        public Dictionary<string, string> map;

        private readonly ControllerType _deviceType;
        private readonly ControllerMappingViewModel _viewModel;
        private bool _loaded;

        private ControllerMappingWindow()
        {
            InitializeComponent();
            _deviceType = ControllerType.Wiimote;
            map = new Dictionary<string, string>();
            _viewModel = new ControllerMappingViewModel(map);
            Loaded += OnLoaded;
        }

        public ControllerMappingWindow(Dictionary<string, string> mappings, ControllerType type)
        {
            InitializeComponent();
            _deviceType = type;
            map = new Dictionary<string, string>(mappings);
            _viewModel = new ControllerMappingViewModel(map);
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

            foreach (var rowModel in _viewModel.Rows)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

                var key = new TextBlock
                {
                    Text = rowModel.Source,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.WrapWholeWords
                };
                Grid.SetColumn(key, 0);

                var value = new TextBox
                {
                    Text = rowModel.Target
                };
                value.TextChanged += (_, _) => rowModel.Target = value.Text ?? string.Empty;
                Grid.SetColumn(value, 1);

                row.Children.Add(key);
                row.Children.Add(value);

                rowsHost.Children.Add(row);
            }
        }

        private void CollectMappings()
        {
            map = _viewModel.ToDictionary();
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            CollectMappings();
            result = true;
        }

        private void btnDefault_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ResetToDefault();
            BuildRows();
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
