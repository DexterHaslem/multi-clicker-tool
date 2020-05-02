using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Controls;

namespace multi_clicker_tool
{
    class ViewModel : BaseNotifyPropertyChanged
    {
        private MainWindow mainWindow;

        public ObservableCollection<SavedClick> SavedClicks { get; private set; }

        public string StatusText { get; set; }
        public string PlayPauseText { get; set; }

        private bool lastAllEnabled;
        private bool lastAllSelected;

        public bool? IsAllEnabled
        {
            get
            {
                if (SavedClicks.All(c => c.IsEnabled))
                    return true;
                if (SavedClicks.All(c => !c.IsEnabled))
                    return false;
                return null;
            }
            set
            {
                foreach (var click in SavedClicks)
                {
                    click.IsEnabled = !lastAllEnabled;
                }
                lastAllEnabled = !lastAllEnabled;
            }
        }

        public bool IsClearAllEnabled { get => SavedClicks.Count > 0; }

        public bool IsDeleteClickEnabled { get => SavedClicks.Any(c => c.IsSelected); }

        public bool? IsAllSelected
        {
            get
            {
                if (SavedClicks.All(c => c.IsSelected))
                    return true;
                if (SavedClicks.All(c => !c.IsSelected))
                    return false;
                return null;
            }
            set
            {
                foreach (var click in SavedClicks)
                {
                    click.IsSelected = !lastAllSelected;
                }
                lastAllSelected = !lastAllSelected;
            }
        }

        public ViewModel(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            StatusText = "123 enabled clicks";
            PlayPauseText = "Paused";

            SavedClicks = new ObservableCollection<SavedClick>();
            SavedClicks.CollectionChanged += OnSavedClicksCollectionChanged;
            for (int i = 0; i < 25; ++i)
            {
                var c = new SavedClick { X = i, Y = i };
                c.EnabledChanged += OnClickEnabledChanged;
                SavedClicks.Add(c);
            }
        }

        private void OnSavedClicksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyPropertyChanged("IsClearAllEnabled");
        }

        private void OnClickEnabledChanged(bool val)
        {
            NotifyPropertyChanged("IsAllEnabled");
        }

        public void ListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NotifyPropertyChanged("IsAllSelected");
            NotifyPropertyChanged("IsDeleteClickEnabled");
        }
    }
}
