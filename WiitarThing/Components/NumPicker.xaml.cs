using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WiinUSoft
{
    public partial class NumPicker : UserControl
    {
        public int Value
        {
            get { return _value; }
            set
            {
                _value = value < _min ? _min : (value > _max ? _max : value);
                if (lblValue != null) lblValue.Text = _value.ToString();
            }
        }

        public int Min
        {
            get { return _min; }
            set
            {
                if (value <= _max)
                {
                    _min = value;
                    if (_value < value) { _value = value; if (lblValue != null) lblValue.Text = _value.ToString(); }
                }
            }
        }

        public int Max
        {
            get { return _max; }
            set
            {
                if (value >= _min)
                {
                    _max = value;
                    if (_value > value) { _value = value; if (lblValue != null) lblValue.Text = _value.ToString(); }
                }
            }
        }

        private int _value = 0;
        private int _min   = 0;
        private int _max   = 100;

        public NumPicker() { InitializeComponent(); }

        public NumPicker(int startValue, int minimum, int maximum) : this()
        {
            _min = minimum; _max = maximum; _value = startValue;
            lblValue.Text = _value.ToString();
        }

        private void btnDown_Click(object sender, RoutedEventArgs e) => Value -= 1;
        private void btnUp_Click(object sender, RoutedEventArgs e)   => Value += 1;

        private void lblValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(lblValue.Text, out int output))
                Value = output;
            else
                lblValue.Text = _value.ToString();
        }
    }
}
