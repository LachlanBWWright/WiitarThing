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

        private ControllerMappingWindow()
        {
            InitializeComponent();
            _deviceType = ControllerType.Wiimote;
            map = new Dictionary<string, string>();
            _viewModel = CreateViewModel(map);
            DataContext = _viewModel;
        }

        public ControllerMappingWindow(Dictionary<string, string> mappings, ControllerType type)
        {
            InitializeComponent();
            _deviceType = type;
            map = new Dictionary<string, string>(mappings);
            _viewModel = CreateViewModel(map);
            DataContext = _viewModel;
        }

        private ControllerMappingViewModel CreateViewModel(Dictionary<string, string> mappings)
            => new(mappings)
            {
                HeaderText = $"Edit mappings for {_deviceType}. Keys on the left map to Xbox targets on the right."
            };

        private void CollectMappings() => map = _viewModel.ToDictionary();

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            CollectMappings();
            result = true;
        }

        private void btnDefault_Click(object sender, RoutedEventArgs e)
            => _viewModel.ResetToDefault();

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
