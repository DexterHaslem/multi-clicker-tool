using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace multi_clicker_tool
{
    public class BaseNotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public delegate void EnabledChangedHandler(bool v);

    public class SavedClick : BaseNotifyPropertyChanged
    {
        private int x;
        private int y;
        private bool isEnabled;
        private bool isSelected;

        public event EnabledChangedHandler EnabledChanged;

        public int X
        {
            get => x;
            set { x = value; NotifyPropertyChanged(); }
        }

        public int Y
        {
            get => y;
            set { y = value; NotifyPropertyChanged(); }
        }

        public bool IsEnabled 
        { 
            get => isEnabled;
            set 
            { 
                isEnabled = value; 
                NotifyPropertyChanged();
                if (EnabledChanged != null)
                    EnabledChanged(value);
            }
        }

        public bool IsSelected
        {
            get => isSelected;
            set { isSelected = value; NotifyPropertyChanged(); }
        }

    }

    

}
