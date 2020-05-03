using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
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

        private bool isRecordingClick;

        private IntPtr mouseHookPtr;
        private IntPtr keyboardHookPtr;
        private NativeMethods.HookProc globalKeyboardHookDelegate;
        private NativeMethods.HookProc globalMouseHookDelegate;
        private readonly IntPtr user32 = NativeMethods.LoadLibrary("user32.dll");
        private ClickRepeatType repeatMode;
        private int repeatCount;
        private int delayMs;
        private bool humanizeClicks;
        private string editSectionToolTip;
        private string startStopButtonText;
        private bool isRecordClickEnabled;

        public ObservableCollection<SavedClick> SavedClicks { get; private set; }

        public int DelayMs { get => delayMs; set { delayMs = value; NotifyPropertyChanged(); } }

        public bool IsRecordClickEnabled { get => isRecordClickEnabled; set { isRecordClickEnabled = value; NotifyPropertyChanged(); } }
        public string StartStopButtonText { get => startStopButtonText; set { startStopButtonText = value; NotifyPropertyChanged(); } }
        public bool IsManualStartStopEnabled { get => !isPlaying && !isRecordingClick; }
        public bool ClickEditIsEnabled { get => !isPlaying && !isRecordingClick; }

        public string EditSectionToolTip { get => editSectionToolTip; set { editSectionToolTip = value; NotifyPropertyChanged(); } }

        public bool HumanizeClicks { get => humanizeClicks; set { humanizeClicks = value; NotifyPropertyChanged(); } }

        public ClickRepeatType RepeatMode { get => repeatMode; set { repeatMode = value; NotifyPropertyChanged(); } }

        public int RepeatCount { get => repeatCount; set { repeatCount = value; NotifyPropertyChanged(); } }

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

        public bool IsClearAllEnabled { get => SavedClicks.Count > 0 && ClickEditIsEnabled; }

        public bool IsDeleteClickEnabled { get => SavedClicks.Any(c => c.IsSelected) && ClickEditIsEnabled; }

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
                UpdateAllUiState();
            }
        }

        public RoutedCommand DeleteClicksCommand { get; private set; }
        public RoutedCommand ClearClicksCommand { get; private set; }
        public RoutedCommand LoadClicksCommand { get; private set; }
        public RoutedCommand SaveClicksCommand { get; private set; }
        public RoutedCommand RecordClickCommand { get; private set; }
        public RoutedCommand ExitCommand { get; private set; }
        public RoutedCommand SetHotkeyCommand { get; private set; }
        public RoutedCommand ManualStartStopCommand { get; private set; }

        public ViewModel(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            StatusText = "123 enabled clicks";
            PlayPauseText = "Paused";

            SavedClicks = new ObservableCollection<SavedClick>();
            SavedClicks.CollectionChanged += OnSavedClicksCollectionChanged;

#if false
            for (int i = 0; i < 25; ++i)
            {
                var c = new SavedClick { X = i, Y = i };
                c.EnabledChanged += OnClickEnabledChanged;
                SavedClicks.Add(c);
            }
#endif

            RepeatMode = ClickRepeatType.Count;
            DeleteClicksCommand = new RoutedCommand("DeleteClicks", typeof(MainWindow));
            ClearClicksCommand = new RoutedCommand("ClearClicks", typeof(MainWindow));
            LoadClicksCommand = new RoutedCommand("LoadClicks", typeof(MainWindow));
            SaveClicksCommand = new RoutedCommand("SaveClicks", typeof(MainWindow));
            RecordClickCommand = new RoutedCommand("RecordClick", typeof(MainWindow));
            ExitCommand = new RoutedCommand("Exit", typeof(MainWindow));
            SetHotkeyCommand = new RoutedCommand("SetHotkey", typeof(MainWindow));
            ManualStartStopCommand = new RoutedCommand("ManualStartStop", typeof(MainWindow));

            mainWindow.CommandBindings.Add(new CommandBinding(ExitCommand, OnExitCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(ClearClicksCommand, OnClearClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(LoadClicksCommand, OnLoadClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(SaveClicksCommand, OnSaveClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(DeleteClicksCommand, OnDeleteClicksCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(RecordClickCommand, OnRecordClickCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(SetHotkeyCommand, OnSetHotkeyCommand));
            mainWindow.CommandBindings.Add(new CommandBinding(ManualStartStopCommand, OnManualStartStopCommand));
            UpdateAllUiState();

            mainWindow.Closing += OnMainWindowClosing;

            SetupKeyboardHook();

            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(typeof(Control), new FrameworkPropertyMetadata(true));
        }

        private void SetupKeyboardHook()
        {
            globalKeyboardHookDelegate = KeyboardHookProc;
            keyboardHookPtr = NativeMethods.SetWindowsHookEx(NativeMethods.HookType.WH_KEYBOARD_LL, globalKeyboardHookDelegate, user32, 0);
        }

        private void OnSetHotkeyCommand(object sender, ExecutedRoutedEventArgs e)
        {
        }

        private void OnManualStartStopCommand(object sender, ExecutedRoutedEventArgs e)
        {
            TogglePlayPause();
        }

        private void TogglePlayPause()
        {
            if (isRecordingClick)
                return;

            isPlaying = !isPlaying;
            UpdateAllUiState();
        }

        private void OnMainWindowClosing(object sender, CancelEventArgs e)
        {
            if (mouseHookPtr != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(mouseHookPtr);
            mouseHookPtr = IntPtr.Zero;

            if (keyboardHookPtr != IntPtr.Zero)
                NativeMethods.UnhookWindowsHookEx(keyboardHookPtr);
            keyboardHookPtr = IntPtr.Zero;

            globalKeyboardHookDelegate = null;
            globalMouseHookDelegate = null;
        }

        private IntPtr KeyboardHookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            //throw new NotImplementedException();44456
            if (!isRecordingClick && code >= 0 && wParam == (IntPtr)NativeMethods.WM_KEYDOWN)
            {
                NativeMethods.KBDLLHOOKSTRUCT kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                Debug.WriteLine($"GLOBAL KEYBOARD HOOK keydown: vkCode = {kbd.vkCode} {kbd.time}");
            }
            return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private IntPtr MouseHookProc(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                //Debug.WriteLine($"GLOBAL MOUSE HOOK: {code} {wParam} {pendingClickRecord}");
                if (isRecordingClick && NativeMethods.WM_LBUTTONDOWN == (uint)wParam)
                {
                    NativeMethods.MSLLHOOKSTRUCT mouseData = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    Debug.WriteLine($"data {code} {wParam} - {mouseData.pt.X} {mouseData.pt.Y}");

                    var nc = new SavedClick { IsEnabled = true, X = mouseData.pt.X, Y = mouseData.pt.Y };
                    SavedClicks.Add(nc);

                    isRecordingClick = false;
                    NotifyPropertyChanged("ClickEditIsEnabled");
                    NotifyPropertyChanged("IsManualStartStopEnabled");
                }
            }

            if (!isRecordingClick)
            {
                NativeMethods.UnhookWindowsHookEx(mouseHookPtr);
                mouseHookPtr = IntPtr.Zero;
            }
            return NativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private void OnRecordClickCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (isRecordingClick || isPlaying)
                return;

            globalMouseHookDelegate = MouseHookProc;
            // TODO: consider whether we need the LL hook for DPI aware crap
            mouseHookPtr = NativeMethods.SetWindowsHookEx(NativeMethods.HookType.WH_MOUSE_LL, globalMouseHookDelegate, user32, 0);
            isRecordingClick = true;
            NotifyPropertyChanged("ClickEditIsEnabled");
            NotifyPropertyChanged("IsManualStartStopEnabled");
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
            if (e.NewItems != null)
            {
                foreach (SavedClick savedClick in e.NewItems)
                    savedClick.EnabledChanged += OnClickEnabledChanged;
            }
            if (e.OldItems != null)
            {
                foreach (SavedClick removedClick in e.OldItems)
                    removedClick.EnabledChanged -= OnClickEnabledChanged;
            }

            NotifyPropertyChanged("IsClearAllEnabled");
            NotifyPropertyChanged("IsDeleteClickEnabled");
            UpdateAllUiState();
        }

        private void OnClickEnabledChanged(bool val)
        {
            NotifyPropertyChanged("IsAllEnabled");
            UpdateAllUiState();
        }

        public void ListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /* NOTE: do not use this event handler for full list update- i think item virtualization is making it fire for
             * only visibile items, so it doesnt work correctly for large lists 
            Debug.WriteLine($"ListBoxSElectionChanged {e.AddedItems.Count} : {e.RemovedItems.Count}");
            NotifyPropertyChanged("IsAllSelected");
            */
            NotifyPropertyChanged("IsDeleteClickEnabled");
            UpdateAllUiState();
        }

        private void UpdateAllUiState()
        {
            var numSelected = SavedClicks.Count(c => c.IsSelected);
            bool isAll = numSelected == 0 || numSelected == SavedClicks.Count;
            EnableCheckText = "Toggle Enabled " + (isAll ? "(All)" : "(Sel)");

            EditSectionToolTip = isPlaying ? "cannot edit clicks while playing, pause first" : "";
            StatusText = "";
            if (SavedClicks.Count(c => c.IsSelected) > 0)
                StatusText = $"Sel: {SavedClicks.Count(c => c.IsSelected)}, ";

            StatusText += $"{SavedClicks.Count(c => c.IsEnabled)} enabled";

            PlayPauseText = isPlaying ? "PLAYING" : "PAUSED";
            StartStopButtonText = isPlaying ? "Pause" : "Play";
            isRecordClickEnabled = !isPlaying;
        }
    }
}
