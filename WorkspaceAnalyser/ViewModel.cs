// MainWindowViewModel.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private int _projectCount;
        private int _controllerCount;
        private string _summaryText;

        public int ProjectCount
        {
            get { return _projectCount; }
            set
            {
                if (_projectCount != value)
                {
                    _projectCount = value;
                    OnPropertyChanged();
                    UpdateSummaryText(); // Update summary when counts change
                }
            }
        }

        public int ControllerCount
        {
            get { return _controllerCount; }
            set
            {
                if (_controllerCount != value)
                {
                    _controllerCount = value;
                    OnPropertyChanged();
                    UpdateSummaryText(); // Update summary when counts change
                }
            }
        }

        public string SummaryText
        {
            get { return _summaryText; }
            set
            {
                if (_summaryText != value)
                {
                    _summaryText = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainWindowViewModel()
        {
            // Initialize with default values
            ProjectCount = 0;
            ControllerCount = 0;
            UpdateSummaryText();
        }

        private void UpdateSummaryText()
        {
            SummaryText = $"Gefundene Projekte: {ProjectCount} | Controller: {ControllerCount}";
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}