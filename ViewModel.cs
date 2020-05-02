using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace multi_clicker_tool
{
    class ViewModel : BaseNotifyPropertyChanged
    {
        private MainWindow mainWindow;
        private bool lastAllEnabled;
        private bool lastAllSelected;
        private string enableCheckText;
        private string playPauseText;
        private string statusText;
        private bool isPlaying;
        private bool pendingClickRecord;

        private IntPtr mouseHookPtr;
        private IntPtr keyboardHookPtr;
        private NativeMethods.HookProc globalKeyboardHookDelegate;
        private NativeMethods.HookProc globalMouseHookDelegate;

        public ObservableCollection<SavedClick> SavedClicks { get; private set; }

        public string StatusText { get => statusText; set { statusText = value; NotifyPropertyChanged(); } }
        public string PlayPauseText { get => playPauseText; set { playPauseText = value; NotifyPropertyChanged(); } }

        public string EnableCheckText { get => enableCheckText; set { enableCheckText = value; NotifyPropertyChanged(); } }

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
                // dont always unconditionally set em all. check selection first
                var selectedClicks = SavedClicks.Where(c => c.IsSelected).ToList();
                bool enableDisableAll = true;
                IEnumerable<SavedClick> toToggle = SavedClicks;
                if (selectedClicks.Count > 0)
                {
                    toToggle = selectedClicks;
                }

                foreach (var click in toToggle)
                {
                    if (enableDisableAll)
                        click.IsEnabled = !lastAllEnabled;
                    else
                        click.IsEnabled = !click.IsEnabled;
                }
                if (enableDisableAll)
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

                NotifyPropertyChanged("IsAllSelected");
                NotifyPropertyChanged("IsDeleteClickEnabled");
                UpdateEnabledCheckboxText();
                UpdateStatusBarText();
            }
        }

        public RoutedCommand DeleteClicksCommand { get; private set; }
        public RoutedCommand ClearClicksCommand { get; private set; }
        public RoutedCommand LoadClicksCommand { get; private set; }
        public RoutedCommand SaveClicksCommand { get; private set; }
        public RoutedCommand RecordClickCommand { get; private set; }
        public RoutedCommand ExitCommand { get; private set; }

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

            DeleteClicksCommand = new RoutedCommand("DeleteClicks", typeof(MainWindow));
            ClearClicksCommand = new RoutedCommand("ClearClicks", typeof(MainWindow));
            LoadClicksCommand = new RoutedCommand("LoadClicks", typeof(MainWindow));
            SaveClicksCommand = new RoutedCommand("SaveClicks", typeof(MainWindow));
            RecordClickCommand = new RoutedCommand("RecordClick", typeof(MainWindow));
            ExitCommand = new RoutedCommand("Exit", typeof(MainWindow));

            mainWindow.CommandBindings.Add(new CommandBinding(ExitCommand, OnExitCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(ClearClicksCommand, OnClearClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(LoadClicksCommand, OnLoadClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(SaveClicksCommand, OnSaveClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(DeleteClicksCommand, OnDeleteClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(RecordClickCommand, OnRecordClickCommand));
            UpdateEnabledCheckboxText();
            UpdateStatusBarText();


            globalKeyboardHookDelegate = KeyboardHookProc;
            var user32 = NativeMethods.LoadLibrary("user32.dll");
            keyboardHookPtr = NativeMethods.SetWindowsHookEx(NativeMethods.HookType.WH_KEYBOARD_LL, globalKeyboardHookDelegate, user32, 0);

            globalMouseHookDelegate = MouseHookProc;
            mouseHookPtr = NativeMethods.SetWindowsHookEx(NativeMethods.HookType.WH_MOUSE_LL, globalMouseHookDelegate, user32, 0);

            //UnhookWindowsHookEx(hook);
        }

        private IntPtr KeyboardHookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            Debug.WriteLine($"GLOBAL KEYBOARD HOOK: {code} {wParam} {lParam}");
            //throw new NotImplementedException();
            return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            Debug.WriteLine($"GLOBAL MOUSE HOOK: {code} {wParam} {lParam}");
            //throw new NotImplementedException();
            return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private void OnRecordClickCommand(object sender, ExecutedRoutedEventArgs e)
        {
            pendingClickRecord = true;
        }

        private void OnClearClicksCommand(object sender, ExecutedRoutedEventArgs e)
        {
        }

        private void OnLoadClicksCommand(object sender, ExecutedRoutedEventArgs e)
        {
        }

        private void OnSaveClicksCommand(object sender, ExecutedRoutedEventArgs e)
        {
        }

        private void OnDeleteClicksCommand(object sender, ExecutedRoutedEventArgs e)
        {
        }

        private void OnExitCommand(object sender, ExecutedRoutedEventArgs e)
        {
            mainWindow.Close();
        }

        private void OnSavedClicksCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            NotifyPropertyChanged("IsClearAllEnabled");
            UpdateEnabledCheckboxText();
            UpdateStatusBarText();
        }

        private void OnClickEnabledChanged(bool val)
        {
            NotifyPropertyChanged("IsAllEnabled");
            UpdateStatusBarText();
        }

        private void UpdateEnabledCheckboxText()
        {
            var numSelected = SavedClicks.Count(c => c.IsSelected);
            bool isAll = numSelected == 0 || numSelected == SavedClicks.Count;
            EnableCheckText = "Toggle Enabled " + (isAll ? "(All)" : "(Sel)");
        }

        public void ListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /* NOTE: do not use this event handler for full list update- i think item virtualization is making it fire for
             * only visibile items, so it doesnt work correctly for large lists 
            Debug.WriteLine($"ListBoxSElectionChanged {e.AddedItems.Count} : {e.RemovedItems.Count}");
            NotifyPropertyChanged("IsAllSelected");
            */
            NotifyPropertyChanged("IsDeleteClickEnabled");
            UpdateEnabledCheckboxText();
            UpdateStatusBarText();
        }

        private void UpdateStatusBarText()
        {
            StatusText = "";
            if (SavedClicks.Count(c => c.IsSelected) > 0)
                StatusText = $"Sel: {SavedClicks.Count(c => c.IsSelected)}, ";
            
            StatusText += $"{SavedClicks.Count(c => c.IsEnabled)} enabled";

            PlayPauseText = isPlaying ? "PLAYING" : "PAUSED";
        }
    }
}
