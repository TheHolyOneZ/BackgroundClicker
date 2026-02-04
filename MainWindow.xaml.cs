using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Serialization;
using BackgroundClicker.Wpf;

namespace BackgroundClicker.Wpf
{
    public partial class MainWindow : Window
    {
        #region Win32 API
        [DllImport("user32.dll")] private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT Point);
        [DllImport("user32.dll")] private static extern IntPtr RealChildWindowFromPoint(IntPtr hwndParent, POINT pt);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
        private const uint GA_ROOT = 2;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        #endregion

        [Serializable]
        public class ClickProfile
        {
            public string Name { get; set; }
            public string WindowTitle { get; set; }
            public long Hwnd { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public double IntervalSeconds { get; set; }
            public bool DoubleClick { get; set; }
            public bool AlwaysOnTop { get; set; }
            public bool AutoRepeat { get; set; }
            public bool HardStop { get; set; }
            public string StopAfterClicks { get; set; }
            public string StopAfterMinutes { get; set; }
            public bool RandomizeInterval { get; set; }
            public string JitterPixels { get; set; }
            public bool PlayClickSound { get; set; }
            public bool PlayErrorSound { get; set; }
            public bool BlockingClick { get; set; }
            public bool UseSequence { get; set; }
            public List<SequenceStep> SequenceSteps { get; set; }
        }

        [Serializable]
        public class SequenceStep
        {
            public string WindowTitle { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public bool DoubleClick { get; set; }
            public double DelaySeconds { get; set; }

            public string ToDisplayString()
            {
                string action = DoubleClick ? "Double click" : "Click";
                return string.Format("{0} at ({1}, {2}) then wait {3:0.###}s", action, X, Y, DelaySeconds);
            }
        }

        private LowLevelMouseProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
        private DispatcherTimer _mouseHoldTimer;
        private DispatcherTimer _clickTimer;
        private IntPtr _targetWindowHandle = IntPtr.Zero;
        private int _totalClicksSent = 0;
        private readonly List<ClickProfile> _profiles = new List<ClickProfile>();
        private string _profilesFilePath;
        private bool _isLoadingProfile;
        private readonly List<SequenceStep> _sequenceSteps = new List<SequenceStep>();
        private int _sequenceIndex = 0;
        private readonly Random _random = new Random();
        private int _sessionClicks = 0;
        private int? _sessionClickLimit = null;
        private DateTime? _sessionEndTimeUtc = null;
        private double _baseIntervalSeconds = 1.0;
        private POINT _lastMousePosition;

        private bool _isProfileDirty = false;
        private bool _isExecutionRunning = false;
        
        private const bool IsBetaBuild = true;

        public MainWindow()
        {
            InitializeComponent();
            this.SourceInitialized += new EventHandler(MainWindow_SourceInitialized);
            _proc = new LowLevelMouseProc(HookCallback);
            ProfileComboBox.Loaded += ProfileComboBox_Loaded;
            _mouseHoldTimer = new DispatcherTimer();
            _mouseHoldTimer.Interval = TimeSpan.FromSeconds(3);
            _mouseHoldTimer.Tick += new EventHandler(MouseHoldTimer_Tick);
            _clickTimer = new DispatcherTimer();
            _clickTimer.Tick += new EventHandler(ClickTimer_Tick);
            _profilesFilePath = GetProfilesFilePath();
            InitializeProfiles();
            this.Closed += new EventHandler(MainWindow_Closed);
            
            if (BetaWatermarkTextBlock != null)
            {
                BetaWatermarkTextBlock.Visibility = IsBetaBuild ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        
        private void SubscribeToProfileChanges()
        {
            WindowTitleTextBox.TextChanged += ProfileField_TextChanged;
            XCoordTextBox.TextChanged += ProfileField_TextChanged;
            YCoordTextBox.TextChanged += ProfileField_TextChanged;
            IntervalTextBox.TextChanged += ProfileField_TextChanged;
            StopAfterClicksTextBox.TextChanged += ProfileField_TextChanged;
            StopAfterMinutesTextBox.TextChanged += ProfileField_TextChanged;
            JitterPixelsTextBox.TextChanged += ProfileField_TextChanged;
            DoubleClickCheckBox.Checked += ProfileField_CheckedChanged;
            DoubleClickCheckBox.Unchecked += ProfileField_CheckedChanged;
            AlwaysOnTopCheckBox.Checked += ProfileField_CheckedChanged;
            AlwaysOnTopCheckBox.Unchecked += ProfileField_CheckedChanged;
            UseSequenceCheckBox.Checked += ProfileField_CheckedChanged;
            UseSequenceCheckBox.Unchecked += ProfileField_CheckedChanged;
            EnableAutoRepeatCheckBox.Checked += ProfileField_CheckedChanged;
            EnableAutoRepeatCheckBox.Unchecked += ProfileField_CheckedChanged;
            BlockingClickCheckBox.Checked += ProfileField_CheckedChanged;
            BlockingClickCheckBox.Unchecked += ProfileField_CheckedChanged;
            HardStopCheckBox.Checked += ProfileField_CheckedChanged;
            HardStopCheckBox.Unchecked += ProfileField_CheckedChanged;
            RandomizeIntervalCheckBox.Checked += ProfileField_CheckedChanged;
            RandomizeIntervalCheckBox.Unchecked += ProfileField_CheckedChanged;
            PlayClickSoundCheckBox.Checked += ProfileField_CheckedChanged;
            PlayClickSoundCheckBox.Unchecked += ProfileField_CheckedChanged;
            PlayErrorSoundCheckBox.Checked += ProfileField_CheckedChanged;
            PlayErrorSoundCheckBox.Unchecked += ProfileField_CheckedChanged;
        }

        private void UnsubscribeFromProfileChanges()
        {
            WindowTitleTextBox.TextChanged -= ProfileField_TextChanged;
            XCoordTextBox.TextChanged -= ProfileField_TextChanged;
            YCoordTextBox.TextChanged -= ProfileField_TextChanged;
            IntervalTextBox.TextChanged -= ProfileField_TextChanged;
            StopAfterClicksTextBox.TextChanged -= ProfileField_TextChanged;
            StopAfterMinutesTextBox.TextChanged -= ProfileField_TextChanged;
            JitterPixelsTextBox.TextChanged -= ProfileField_TextChanged;
            DoubleClickCheckBox.Checked -= ProfileField_CheckedChanged;
            DoubleClickCheckBox.Unchecked -= ProfileField_CheckedChanged;
            AlwaysOnTopCheckBox.Checked -= ProfileField_CheckedChanged;
            AlwaysOnTopCheckBox.Unchecked -= ProfileField_CheckedChanged;
            UseSequenceCheckBox.Checked -= ProfileField_CheckedChanged;
            UseSequenceCheckBox.Unchecked -= ProfileField_CheckedChanged;
            EnableAutoRepeatCheckBox.Checked -= ProfileField_CheckedChanged;
            EnableAutoRepeatCheckBox.Unchecked -= ProfileField_CheckedChanged;
            BlockingClickCheckBox.Checked -= ProfileField_CheckedChanged;
            BlockingClickCheckBox.Unchecked -= ProfileField_CheckedChanged;
            HardStopCheckBox.Checked -= ProfileField_CheckedChanged;
            HardStopCheckBox.Unchecked -= ProfileField_CheckedChanged;
            RandomizeIntervalCheckBox.Checked -= ProfileField_CheckedChanged;
            RandomizeIntervalCheckBox.Unchecked -= ProfileField_CheckedChanged;
            PlayClickSoundCheckBox.Checked -= ProfileField_CheckedChanged;
            PlayClickSoundCheckBox.Unchecked -= ProfileField_CheckedChanged;
            PlayErrorSoundCheckBox.Checked -= ProfileField_CheckedChanged;
            PlayErrorSoundCheckBox.Unchecked -= ProfileField_CheckedChanged;
        }
        
        private void SetProfileDirty(bool isDirty)
        {
            if (_isProfileDirty == isDirty)
            {
                return;
            }

            _isProfileDirty = isDirty;
            UpdateRunButtonState();
        }

        private void UpdateRunButtonState()
        {
            if (_isExecutionRunning)
            {
                return;
            }
            
            ExecutionButton.IsEnabled = true;

            if (_isProfileDirty)
            {
                UpdateStatus("Profile has unsaved changes. You can save it or start anyway.", true);
            }
            else
            {
                string statusText = StatusTextBlock.Text ?? string.Empty;
                if (statusText.IndexOf("unsaved changes", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UpdateStatus("Profile saved. Ready to run.", false);
                }
            }
        }

        private void SetUiEnabledState(bool isEnabled)
        {
            ProfileComboBox.IsEnabled = isEnabled;
            SaveProfileButton.IsEnabled = isEnabled;
            DeleteProfileButton.IsEnabled = isEnabled;
            WindowTitleTextBox.IsEnabled = isEnabled;
            XCoordTextBox.IsEnabled = isEnabled;
            YCoordTextBox.IsEnabled = isEnabled;
            SequenceListBox.IsEnabled = isEnabled;
            SequenceDelayTextBox.IsEnabled = isEnabled;
            AddSequenceStepButton.IsEnabled = isEnabled;
            RemoveSequenceStepButton.IsEnabled = isEnabled;
            ClearSequenceButton.IsEnabled = isEnabled;
            AutoSetupButton.IsEnabled = isEnabled;
            IntervalTextBox.IsEnabled = isEnabled;
            RandomizeIntervalCheckBox.IsEnabled = isEnabled;
            StopAfterClicksTextBox.IsEnabled = isEnabled;
            StopAfterMinutesTextBox.IsEnabled = isEnabled;
            JitterPixelsTextBox.IsEnabled = isEnabled;
            PlayClickSoundCheckBox.IsEnabled = isEnabled;
            PlayErrorSoundCheckBox.IsEnabled = isEnabled;
            DoubleClickCheckBox.IsEnabled = isEnabled;
            AlwaysOnTopCheckBox.IsEnabled = isEnabled;
            UseSequenceCheckBox.IsEnabled = isEnabled;
            EnableAutoRepeatCheckBox.IsEnabled = isEnabled;
            BlockingClickCheckBox.IsEnabled = isEnabled;
            HardStopCheckBox.IsEnabled = isEnabled;
        }

        private bool ValidateConfigurationForRun()
        {
            bool useSequence = UseSequenceCheckBox.IsChecked == true;

            if (useSequence)
            {
                if (_sequenceSteps.Count == 0)
                {
                    ShowNotification("Your sequence is enabled but has no steps. Add at least one step before starting.", true);
                    return false;
                }
            }
            else
            {
                string title = (WindowTitleTextBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(title))
                {
                    ShowNotification("Please enter a target window title or enable a sequence before starting.", true);
                    return false;
                }

                int x;
                int y;
                if (!int.TryParse(XCoordTextBox.Text, out x) || !int.TryParse(YCoordTextBox.Text, out y))
                {
                    ShowNotification("X and Y coordinates must be valid integers.", true);
                    return false;
                }

                if (x == 0 && y == 0)
                {
                    ShowNotification("X and Y cannot both be 0. Move the target point away from the top-left corner.", true);
                    return false;
                }
                
                IntPtr testHandle = FindWindowByApproxTitle(title);
                if (testHandle == IntPtr.Zero)
                {
                    ShowNotification("Could not find any window matching the title '" + title + "'. Make sure it is open and visible, or use the target capture.", true);
                    return false;
                }
                
                _targetWindowHandle = testHandle;
            }

            if (EnableAutoRepeatCheckBox.IsChecked == true)
            {
                double sec;
                if (!double.TryParse(IntervalTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out sec) || sec <= 0)
                {
                    ShowNotification("Interval must be a number greater than 0 seconds.", true);
                    return false;
                }
            }

            return true;
        }

        private void ExecutionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isExecutionRunning && _isProfileDirty)
            {
                var dialog = new NotificationWindow(
                    "You changed this profile. Save it before starting?",
                    false,
                    "Save and Start",
                    "Cancel")
                {
                    Owner = this
                };

                bool? dialogResult = dialog.ShowDialog();
                if (dialogResult == true && dialog.Result == NotificationResult.Primary)
                {
                    SaveProfileButton_Click(this, new RoutedEventArgs());
                    if (_isProfileDirty)
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            
            if (!_isExecutionRunning)
            {
                if (!ValidateConfigurationForRun())
                {
                    return;
                }
            }

            _isExecutionRunning = !_isExecutionRunning;

            if (_isExecutionRunning)
            {
                UpdateStatus("Running...", false);
                ExecutionButton.Content = "STOP";
                ExecutionButton.Background = new SolidColorBrush(Colors.DarkRed);
                ExecutionButton.BorderBrush = new SolidColorBrush(Colors.Red);
                SetUiEnabledState(false);

                if (EnableAutoRepeatCheckBox.IsChecked == true)
                {
                    double sec;
                    if (double.TryParse(IntervalTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out sec) && sec > 0)
                    {
                        _baseIntervalSeconds = sec;
                        _sessionClicks = 0;
                        _sessionClickLimit = null;
                        _sessionEndTimeUtc = null;

                        int clicksLimit;
                        if (int.TryParse(StopAfterClicksTextBox.Text, out clicksLimit) && clicksLimit > 0)
                        {
                            _sessionClickLimit = clicksLimit;
                        }

                        double minutesLimit;
                        if (double.TryParse(StopAfterMinutesTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out minutesLimit) && minutesLimit > 0)
                        {
                            _sessionEndTimeUtc = DateTime.UtcNow.AddMinutes(minutesLimit);
                        }

                        _clickTimer.Interval = TimeSpan.FromMilliseconds(sec * 1000);
                        _clickTimer.Start();
                    }
                    else
                    {
                        UpdateStatus("Invalid Interval", true);
                        _isExecutionRunning = false;
                        ExecutionButton_Click(this, new RoutedEventArgs());
                    }
                }
                else
                {
                    bool useSequence = UseSequenceCheckBox.IsChecked == true && _sequenceSteps.Count > 0;
                    if (useSequence)
                    {
                        RunSequenceOnce();
                    }
                    else
                    {
                        TrySendBackgroundClick();
                    }
                    ExecutionButton_Click(this, new RoutedEventArgs());
                }
            }
            else
            {
                _clickTimer.Stop();
                UpdateStatus("Stopped by user.", false);
                ExecutionButton.Content = "START";
                ExecutionButton.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x8B, 0x22));
                ExecutionButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0xB3, 0x71));
                SetUiEnabledState(true);
            }
        }

        private static List<SequenceStep> CloneSequenceSteps(IEnumerable<SequenceStep> source)
        {
            if (source == null) return new List<SequenceStep>();
            return source.Select(s => new SequenceStep { WindowTitle = s.WindowTitle, X = s.X, Y = s.Y, DoubleClick = s.DoubleClick, DelaySeconds = s.DelaySeconds }).ToList();
        }

        private IntPtr FindWindowByApproxTitle(string savedTitle)
        {
            if (string.IsNullOrWhiteSpace(savedTitle)) return IntPtr.Zero;
            string target = savedTitle.Trim();

            IntPtr found = FindWindow(null, target);
            if (found != IntPtr.Zero && IsWindow(found) && IsWindowVisible(found))
            {
                return found;
            }
            
            found = IntPtr.Zero;

            try
            {
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindow(hWnd) || !IsWindowVisible(hWnd)) return true;
                    
                    int len = GetWindowTextLength(hWnd);
                    if (len <= 0) return true;
                    
                    StringBuilder sb = new StringBuilder(len + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    string current = sb.ToString();

                    if (string.IsNullOrWhiteSpace(current)) return true;
                    
                    if (current.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 || target.IndexOf(current, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = hWnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { found = IntPtr.Zero; }
            return found;
        }

        private string GetProfilesFilePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BackgroundClickerWpf");
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch
            {
                dir = AppDomain.CurrentDomain.BaseDirectory;
            }
            return Path.Combine(dir, "profiles.xml");
        }

        private void InitializeProfiles()
        {
            LoadProfiles();
            RefreshSequenceListBox();
            if (_profiles.Count == 0)
            {
                var defaultProfile = CreateProfileFromCurrent("Default");
                _profiles.Add(defaultProfile);
                SaveProfiles();
            }
            string initialName = _profiles.Count > 0 ? _profiles[0].Name : null;
            RefreshProfilesCombo(initialName);
            var selected = ProfileComboBox.SelectedItem as ClickProfile;
            if (selected != null)
            {
                LoadProfileIntoUi(selected);
            }
            SubscribeToProfileChanges();
            SetProfileDirty(false);
        }

        private void LoadProfiles()
        {
            _profiles.Clear();
            if (string.IsNullOrEmpty(_profilesFilePath) || !File.Exists(_profilesFilePath)) return;
            try
            {
                using (var stream = File.OpenRead(_profilesFilePath))
                {
                    var serializer = new XmlSerializer(typeof(List<ClickProfile>));
                    var loaded = serializer.Deserialize(stream) as List<ClickProfile>;
                    if (loaded != null)
                    {
                        _profiles.AddRange(loaded);
                    }
                }
            }
            catch { }
        }

        private void SaveProfiles()
        {
            if (string.IsNullOrEmpty(_profilesFilePath)) return;
            try
            {
                using (var stream = File.Create(_profilesFilePath))
                {
                    var serializer = new XmlSerializer(typeof(List<ClickProfile>));
                    serializer.Serialize(stream, _profiles);
                }
                SetProfileDirty(false);
            }
            catch { }
        }
        
        private ClickProfile CreateProfileFromCurrent(string name)
        {
            double intervalSeconds;
            double.TryParse(IntervalTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out intervalSeconds);
            int x;
            int.TryParse(XCoordTextBox.Text, out x);
            int y;
            int.TryParse(YCoordTextBox.Text, out y);

            return new ClickProfile
            {
                Name = name,
                WindowTitle = (WindowTitleTextBox.Text ?? string.Empty).Trim(),
                Hwnd = _targetWindowHandle.ToInt64(),
                X = x,
                Y = y,
                IntervalSeconds = intervalSeconds > 0 ? intervalSeconds : 1.0,
                DoubleClick = DoubleClickCheckBox.IsChecked == true,
                AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true,
                AutoRepeat = EnableAutoRepeatCheckBox.IsChecked == true,
                HardStop = HardStopCheckBox.IsChecked == true,
                StopAfterClicks = StopAfterClicksTextBox.Text,
                StopAfterMinutes = StopAfterMinutesTextBox.Text,
                RandomizeInterval = RandomizeIntervalCheckBox.IsChecked == true,
                JitterPixels = JitterPixelsTextBox.Text,
                PlayClickSound = PlayClickSoundCheckBox.IsChecked == true,
                PlayErrorSound = PlayErrorSoundCheckBox.IsChecked == true,
                BlockingClick = BlockingClickCheckBox.IsChecked == true,
                UseSequence = UseSequenceCheckBox.IsChecked == true,
                SequenceSteps = CloneSequenceSteps(_sequenceSteps)
            };
        }
        
        private void UpdateProfileFromCurrent(ClickProfile profile)
        {
            if (profile == null) return;
            double intervalSeconds;
            double.TryParse(IntervalTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out intervalSeconds);
            int x;
            int.TryParse(XCoordTextBox.Text, out x);
            int y;
            int.TryParse(YCoordTextBox.Text, out y);

            profile.WindowTitle = (WindowTitleTextBox.Text ?? string.Empty).Trim();
            profile.Hwnd = _targetWindowHandle.ToInt64();
            profile.X = x;
            profile.Y = y;
            profile.IntervalSeconds = intervalSeconds > 0 ? intervalSeconds : 1.0;
            profile.DoubleClick = DoubleClickCheckBox.IsChecked == true;
            profile.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
            profile.AutoRepeat = EnableAutoRepeatCheckBox.IsChecked == true;
            profile.HardStop = HardStopCheckBox.IsChecked == true;
            profile.StopAfterClicks = StopAfterClicksTextBox.Text;
            profile.StopAfterMinutes = StopAfterMinutesTextBox.Text;
            profile.RandomizeInterval = RandomizeIntervalCheckBox.IsChecked == true;
            profile.JitterPixels = JitterPixelsTextBox.Text;
            profile.PlayClickSound = PlayClickSoundCheckBox.IsChecked == true;
            profile.PlayErrorSound = PlayErrorSoundCheckBox.IsChecked == true;
            profile.BlockingClick = BlockingClickCheckBox.IsChecked == true;
            profile.UseSequence = UseSequenceCheckBox.IsChecked == true;
            profile.SequenceSteps = CloneSequenceSteps(_sequenceSteps);
        }
        
        private void LoadProfileIntoUi(ClickProfile profile)
        {
            if (profile == null) return;
            _isLoadingProfile = true;
            UnsubscribeFromProfileChanges();
            try
            {
                WindowTitleTextBox.Text = profile.WindowTitle ?? string.Empty;
                XCoordTextBox.Text = profile.X.ToString();
                YCoordTextBox.Text = profile.Y.ToString();
                IntervalTextBox.Text = profile.IntervalSeconds.ToString(CultureInfo.InvariantCulture);
                DoubleClickCheckBox.IsChecked = profile.DoubleClick;
                AlwaysOnTopCheckBox.IsChecked = profile.AlwaysOnTop;
                EnableAutoRepeatCheckBox.IsChecked = profile.AutoRepeat;
                HardStopCheckBox.IsChecked = profile.HardStop;
                StopAfterClicksTextBox.Text = profile.StopAfterClicks;
                StopAfterMinutesTextBox.Text = profile.StopAfterMinutes;
                RandomizeIntervalCheckBox.IsChecked = profile.RandomizeInterval;
                JitterPixelsTextBox.Text = profile.JitterPixels;
                PlayClickSoundCheckBox.IsChecked = profile.PlayClickSound;
                PlayErrorSoundCheckBox.IsChecked = profile.PlayErrorSound;
                BlockingClickCheckBox.IsChecked = profile.BlockingClick;
                UseSequenceCheckBox.IsChecked = profile.UseSequence;
                ProfileComboBox.Text = profile.Name ?? string.Empty;

                IntPtr hwnd = (IntPtr)profile.Hwnd;
                if (hwnd != IntPtr.Zero && IsWindow(hwnd))
                {
                    _targetWindowHandle = hwnd;
                    UpdateStatus("Target window ready (HWND).", false);
                }
                else
                {
                    _targetWindowHandle = FindWindowByApproxTitle(profile.WindowTitle);
                    if (_targetWindowHandle != IntPtr.Zero)
                    {
                        UpdateStatus("Target window ready (Title).", false);
                    }
                    else
                    {
                        UpdateStatus("Target window not found. Use 'Setup'.", true);
                    }
                }

                _sequenceSteps.Clear();
                _sequenceIndex = 0;
                if (profile.SequenceSteps != null)
                {
                    _sequenceSteps.AddRange(CloneSequenceSteps(profile.SequenceSteps));
                }
                RefreshSequenceListBox();
            }
            finally
            {
                _isLoadingProfile = false;
                SubscribeToProfileChanges();
                SetProfileDirty(false);
            }
        }
        
        private void RefreshProfilesCombo(string selectedName)
        {
            _isLoadingProfile = true;
            try
            {
                ProfileComboBox.ItemsSource = null;
                if (_profiles.Any())
                {
                    ProfileComboBox.ItemsSource = _profiles;
                    ProfileComboBox.DisplayMemberPath = "Name";
                    ClickProfile match = null;
                    if (selectedName != null)
                    {
                        match = _profiles.FirstOrDefault(p => string.Equals(p.Name, selectedName, StringComparison.OrdinalIgnoreCase));
                    }
                    ProfileComboBox.SelectedItem = match ?? _profiles.FirstOrDefault();
                }
            }
            finally { _isLoadingProfile = false; }
        }

        private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingProfile || _isExecutionRunning) return;

            if (_isProfileDirty)
            {
                MessageBoxResult result = MessageBox.Show("You have unsaved changes in the current profile. Save them before switching?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    SaveProfileButton_Click(this, new RoutedEventArgs());
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    _isLoadingProfile = true;
                    ProfileComboBox.SelectedItem = _profiles.FirstOrDefault(p => p.Name == (ProfileComboBox.Text ?? ""));
                    _isLoadingProfile = false;
                    return;
                }
            }
            
            if (ProfileComboBox.SelectedItem is ClickProfile)
            {
                LoadProfileIntoUi((ClickProfile)ProfileComboBox.SelectedItem);
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            string name = (ProfileComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                name = "Profile " + (_profiles.Count + 1);
            }
            var existing = _profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                _profiles.Add(CreateProfileFromCurrent(name));
            }
            else
            {
                UpdateProfileFromCurrent(existing);
            }
            SaveProfiles();
            RefreshProfilesCombo(name);
            UpdateStatus("Profile saved: " + name, false);
            ShowNotification("Profile saved: " + name, false);
        }

        private void ProfileField_TextChanged(object sender, TextChangedEventArgs e) { if (!_isLoadingProfile) SetProfileDirty(true); }
        private void ProfileField_CheckedChanged(object sender, RoutedEventArgs e) { if (!_isLoadingProfile) SetProfileDirty(true); }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(ProfileComboBox.SelectedItem is ClickProfile)) return;
            ClickProfile selected = (ClickProfile)ProfileComboBox.SelectedItem;
            _profiles.Remove(selected);
            SaveProfiles();
            string nextName = _profiles.Count > 0 ? _profiles[0].Name : null;
            RefreshProfilesCombo(nextName);
            if (ProfileComboBox.SelectedItem is ClickProfile)
            {
                LoadProfileIntoUi((ClickProfile)ProfileComboBox.SelectedItem);
            }
            else
            {
                LoadProfileIntoUi(new ClickProfile { Name = "Default", IntervalSeconds = 1.0 });
                SetProfileDirty(true);
            }
        }

        private void RefreshSequenceListBox()
        {
            if (SequenceListBox == null) return;
            SequenceListBox.ItemsSource = _sequenceSteps.Select(s => s.ToDisplayString()).ToList();
        }

        private void AddSequenceStepButton_Click(object sender, RoutedEventArgs e)
        {
            int x, y;
            if (!int.TryParse(XCoordTextBox.Text, out x) || !int.TryParse(YCoordTextBox.Text, out y))
            {
                UpdateStatus("Invalid coordinates for step.", true);
                return;
            }
            double delay;
            double.TryParse(SequenceDelayTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out delay);
            _sequenceSteps.Add(new SequenceStep
            {
                WindowTitle = (WindowTitleTextBox.Text ?? string.Empty).Trim(),
                X = x,
                Y = y,
                DoubleClick = DoubleClickCheckBox.IsChecked == true,
                DelaySeconds = delay
            });
            RefreshSequenceListBox();
            SetProfileDirty(true);
        }

        private void RemoveSequenceStepButton_Click(object sender, RoutedEventArgs e)
        {
            int index = SequenceListBox.SelectedIndex;
            if (index < 0 || index >= _sequenceSteps.Count) return;
            _sequenceSteps.RemoveAt(index);
            if (_sequenceIndex >= _sequenceSteps.Count)
            {
                _sequenceIndex = 0;
            }
            RefreshSequenceListBox();
            SetProfileDirty(true);
        }

        private void ClearSequenceButton_Click(object sender, RoutedEventArgs e)
        {
            _sequenceSteps.Clear();
            _sequenceIndex = 0;
            RefreshSequenceListBox();
            SetProfileDirty(true);
        }

        private void SequenceListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_isExecutionRunning || SequenceListBox.SelectedItem == null)
            {
                return;
            }

            int selectedIndex = SequenceListBox.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _sequenceSteps.Count)
            {
                var stepToEdit = _sequenceSteps[selectedIndex];
                var editWindow = new EditStepWindow(stepToEdit)
                {
                    Owner = this
                };

                if (editWindow.ShowDialog() == true)
                {
                    RefreshSequenceListBox();
                    SetProfileDirty(true);
                }
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }

        private void ProfileComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var editableTextBox = ProfileComboBox.Template.FindName("PART_EditableTextBox", ProfileComboBox) as TextBox;
            if (editableTextBox != null)
            {
                editableTextBox.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x15));
                editableTextBox.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xF2, 0xEA));
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new HelpWindow();
            help.Owner = this;
            help.ShowDialog();
        }

        private void AutoClickerButton_Click(object sender, RoutedEventArgs e)
        {
            var autoClicker = new AutoClickerWindow
            {
                Owner = this
            };
            autoClicker.Show();
        }

        private void CreditsButton_Click(object sender, RoutedEventArgs e)
        {
            var credits = new CreditsWindow();
            credits.Owner = this;
            credits.ShowDialog();
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }

        private void UpdateStatus(string message, bool isError)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = isError ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Color.FromRgb(0, 242, 234));
        }

        private async void RunSequenceOnce()
        {
            if (_sequenceSteps.Count == 0) return;

            try
            {
                POINT lastScreenPoint = new POINT();
                IntPtr seqHWnd = IntPtr.Zero;

                if (BlockingClickCheckBox.IsChecked == true)
                {
                    GetCursorPos(out lastScreenPoint);
                    if (_targetWindowHandle != IntPtr.Zero && IsWindow(_targetWindowHandle))
                    {
                        seqHWnd = _targetWindowHandle;
                    }
                    else
                    {
                        string windowName = (WindowTitleTextBox.Text ?? "").Trim();
                        if (!string.IsNullOrEmpty(windowName))
                        {
                            seqHWnd = FindWindow(null, windowName);
                            if (seqHWnd == IntPtr.Zero)
                            {
                                seqHWnd = FindWindowByApproxTitle(windowName);
                            }
                        }
                    }

                    if (seqHWnd == IntPtr.Zero)
                    {
                        UpdateStatus("Target window not found for blocking sequence.", true);
                        return;
                    }
                }

                for (int i = 0; i < _sequenceSteps.Count; i++)
                {
                    var step = _sequenceSteps[i];
                    if (BlockingClickCheckBox.IsChecked == true)
                    {
                        POINT targetScreenPoint = new POINT { X = step.X, Y = step.Y };
                        ClientToScreen(seqHWnd, ref targetScreenPoint);
                        SlideMouse(lastScreenPoint, targetScreenPoint, 20, 200);
                        SendBlockingClick(targetScreenPoint.X, targetScreenPoint.Y, step.DoubleClick);
                        lastScreenPoint = targetScreenPoint;
                    }
                    else
                    {
                        UnsubscribeFromProfileChanges();
                        WindowTitleTextBox.Text = step.WindowTitle;
                        XCoordTextBox.Text = step.X.ToString();
                        YCoordTextBox.Text = step.Y.ToString();
                        DoubleClickCheckBox.IsChecked = step.DoubleClick;
                        SubscribeToProfileChanges();
                        TrySendBackgroundClick();
                    }

                    if (i < _sequenceSteps.Count - 1)
                    {
                        double delay = step.DelaySeconds;
                        if (delay <= 0)
                        {
                           delay = _baseIntervalSeconds > 0 ? _baseIntervalSeconds : 1.0;
                        }
                        await Task.Delay((int)(delay * 1000)).ConfigureAwait(true);
                    }
                }
            }
            finally
            {
                _sequenceIndex = 0;
            }
        }

        private void SendScreenClickAt(int x, int y, bool doubleClick)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, UIntPtr.Zero);
            if (doubleClick)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, UIntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, UIntPtr.Zero);
            }
        }

        private void SendBlockingClick(int x, int y, bool doubleClick)
        {
            GetCursorPos(out _lastMousePosition);
            SendScreenClickAt(x, y, doubleClick);
            SetCursorPos(_lastMousePosition.X, _lastMousePosition.Y);
        }

        private void SlideMouse(POINT start, POINT end, int steps, int duration)
        {
            if (steps <= 0) steps = 1;
            int sleep = duration / steps;
            for (int i = 0; i <= steps; i++)
            {
                float progress = (float)i / steps;
                int newX = (int)(start.X + (end.X - start.X) * progress);
                int newY = (int)(start.Y + (end.Y - start.Y) * progress);
                SetCursorPos(newX, newY);
                Thread.Sleep(sleep);
            }
        }

        private bool TrySendBackgroundClick()
        {
            int x, y;
            if (!int.TryParse(XCoordTextBox.Text, out x) || !int.TryParse(YCoordTextBox.Text, out y))
            {
                UpdateStatus("Invalid Coordinates", true);
                return false;
            }

            IntPtr hWnd = IntPtr.Zero;
            if (_targetWindowHandle != IntPtr.Zero && IsWindow(_targetWindowHandle))
            {
                hWnd = _targetWindowHandle;
            }
            else
            {
                 hWnd = FindWindowByApproxTitle((WindowTitleTextBox.Text ?? "").Trim());
            }

            if (hWnd == IntPtr.Zero)
            {
                UpdateStatus("Target window not found. Check the title or bring the window to the foreground.", true);

                if (_isExecutionRunning)
                {
                    if (PlayErrorSoundCheckBox.IsChecked == true)
                    {
                        SystemSounds.Hand.Play();
                    }
                    ExecutionButton_Click(this, new RoutedEventArgs());
                }
                else
                {
                    ShowNotification("Target window not found. Verify the window title or use the setup capture.", true);
                }

                return false;
            }

            int jitter;
            if (int.TryParse(JitterPixelsTextBox.Text, out jitter) && jitter > 0)
            {
                x += _random.Next(-jitter, jitter + 1);
                y += _random.Next(-jitter, jitter + 1);
            }

            if (BlockingClickCheckBox.IsChecked == true)
            {
                var point = new POINT { X = x, Y = y };
                ClientToScreen(hWnd, ref point);
                
                POINT originalPos;
                GetCursorPos(out originalPos);

                SlideMouse(originalPos, point, 20, 150);
                SendScreenClickAt(point.X, point.Y, DoubleClickCheckBox.IsChecked == true);
                SlideMouse(point, originalPos, 20, 150);
            }
            else
            {
                IntPtr lParam = (IntPtr)((y << 16) | (x & 0xFFFF));
                SendMessage(hWnd, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                if (DoubleClickCheckBox.IsChecked == true)
                {
                    SendMessage(hWnd, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                    SendMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
                }
            }
            _totalClicksSent++;
            ClickCountTextBlock.Text = _totalClicksSent.ToString();
            if (PlayClickSoundCheckBox.IsChecked == true)
            {
                SystemSounds.Asterisk.Play();
            }
            UpdateStatus("Click Sent Successfully", false);
            return true;
        }

        private bool RunSequenceTick()
        {
            if (_sequenceSteps.Count == 0) return false;
            if (_sequenceIndex < 0 || _sequenceIndex >= _sequenceSteps.Count)
            {
                _sequenceIndex = 0;
            }
            var step = _sequenceSteps[_sequenceIndex];
            bool sent = false;

            if (BlockingClickCheckBox.IsChecked == true)
            {
                 IntPtr seqHWnd = IntPtr.Zero;
                 if (_targetWindowHandle != IntPtr.Zero && IsWindow(_targetWindowHandle))
                 {
                     seqHWnd = _targetWindowHandle;
                 }
                 else
                 {
                     seqHWnd = FindWindowByApproxTitle((WindowTitleTextBox.Text ?? "").Trim());
                 }

                if (seqHWnd == IntPtr.Zero)
                {
                    UpdateStatus("Target window lost. Stopping.", true);
                    ExecutionButton_Click(this, new RoutedEventArgs());
                    return false;
                }
                if (_sequenceIndex == 0)
                {
                    GetCursorPos(out _lastMousePosition);
                }
                POINT targetScreenPoint = new POINT { X = step.X, Y = step.Y };
                ClientToScreen(seqHWnd, ref targetScreenPoint);
                SlideMouse(_lastMousePosition, targetScreenPoint, 20, 200);
                SendBlockingClick(targetScreenPoint.X, targetScreenPoint.Y, step.DoubleClick);
                _lastMousePosition = targetScreenPoint;
                sent = true;
            }
            else
            {
                UnsubscribeFromProfileChanges();
                WindowTitleTextBox.Text = step.WindowTitle;
                XCoordTextBox.Text = step.X.ToString();
                YCoordTextBox.Text = step.Y.ToString();
                DoubleClickCheckBox.IsChecked = step.DoubleClick;
                SubscribeToProfileChanges();
                sent = TrySendBackgroundClick();
            }

            _sequenceIndex = (_sequenceIndex + 1) % _sequenceSteps.Count;
            if (_isExecutionRunning)
            {
                double delay = step.DelaySeconds;
                if(delay <= 0)
                {
                   delay = _baseIntervalSeconds > 0 ? _baseIntervalSeconds : 1.0;
                }
                _clickTimer.Interval = TimeSpan.FromMilliseconds(delay * 1000.0);
            }
            return sent;
        }

        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            bool sent = false;
            if (UseSequenceCheckBox.IsChecked == true && _sequenceSteps.Count > 0)
            {
                sent = RunSequenceTick();
            }
            else
            {
                sent = TrySendBackgroundClick();
            }
            
            if (sent)
            {
                _sessionClicks++;
                
                if (HardStopCheckBox.IsChecked == true)
                {
                    bool hardClickLimitReached = _sessionClickLimit.HasValue && _sessionClicks >= _sessionClickLimit.Value;
                    if (hardClickLimitReached)
                    {
                        UpdateStatus("Hard click limit reached. Stopping.", false);
                        if (PlayErrorSoundCheckBox.IsChecked == true) System.Media.SystemSounds.Hand.Play();
                        ExecutionButton_Click(this, new RoutedEventArgs());
                        return; 
                    }
                }

                bool clickLimitReached = _sessionClickLimit.HasValue && _sessionClicks >= _sessionClickLimit.Value;
                bool timeLimitReached = _sessionEndTimeUtc.HasValue && DateTime.UtcNow >= _sessionEndTimeUtc.Value;

                bool isReadyForSoftStopCheck = !UseSequenceCheckBox.IsChecked.GetValueOrDefault()
                                               || _sequenceSteps.Count == 0
                                               || _sequenceIndex == 0;

                if (isReadyForSoftStopCheck && (clickLimitReached || timeLimitReached))
                {
                    UpdateStatus("Run limit reached. Stopping.", false);
                    if (PlayErrorSoundCheckBox.IsChecked == true)
                    {
                        SystemSounds.Hand.Play();
                    }
                    ExecutionButton_Click(this, new RoutedEventArgs());
                }
                else if (RandomizeIntervalCheckBox.IsChecked == true && _baseIntervalSeconds > 0)
                {
                    double factor = 1.0 + ((_random.NextDouble() * 0.4) - 0.2);
                    _clickTimer.Interval = TimeSpan.FromMilliseconds(_baseIntervalSeconds * factor * 1000.0);
                }
            }
        }

        private void IntervalPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                IntervalTextBox.Text = ((Button)sender).Tag.ToString();
            }
        }

        private void AutoSetupButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatus("Hold Left Mouse for 3 Seconds on target...", false);
            if (_hookID != IntPtr.Zero) UnhookWindowsHookEx(_hookID);
            _hookID = SetHook(_proc);
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(14, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if ((uint)wParam == WM_LBUTTONDOWN)
                {
                    _mouseHoldTimer.Start();
                }
                else if ((uint)wParam == WM_LBUTTONUP)
                {
                    _mouseHoldTimer.Stop();
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void MouseHoldTimer_Tick(object sender, EventArgs e)
        {
            _mouseHoldTimer.Stop();
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }

            POINT pos;
            if (GetCursorPos(out pos))
            {
                IntPtr hWnd = WindowFromPoint(pos);
                if (hWnd != IntPtr.Zero)
                {
                    hWnd = GetAncestor(hWnd, GA_ROOT);
                }

                if (hWnd != IntPtr.Zero)
                {
                    _targetWindowHandle = hWnd;
                    StringBuilder sb = new StringBuilder(GetWindowTextLength(hWnd) + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);

                    UnsubscribeFromProfileChanges();
                    WindowTitleTextBox.Text = sb.ToString();
                    ScreenToClient(hWnd, ref pos);
                    XCoordTextBox.Text = pos.X.ToString();
                    YCoordTextBox.Text = pos.Y.ToString();
                    SubscribeToProfileChanges();

                    SetProfileDirty(true);
                    UpdateStatus("Target Captured! Please save the profile.", false);
                }
                else
                {
                    UpdateStatus("Error: No Window Detected", true);
                }
            }
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            if (source != null)
            {
                source.AddHook(HwndHook);
            }
            RegisterHotKey(helper.Handle, 1, 0, 0x77); 
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312 && wParam.ToInt32() == 1) 
            {
                if (ExecutionButton.IsEnabled)
                {
                    ExecutionButton_Click(this, new RoutedEventArgs());
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if (_hookID != IntPtr.Zero) UnhookWindowsHookEx(_hookID);
            _clickTimer.Stop();
            
            if (Application.Current != null)
            {
                Application.Current.Shutdown();
            }
        }

        private void ShowNotification(string message, bool isError = false)
        {
            try
            {
                var window = new NotificationWindow(message, isError)
                {
                    Owner = this
                };
                window.ShowDialog();
            }
            catch
            {
                MessageBox.Show(message,
                    isError ? "Error" : "Information",
                    MessageBoxButton.OK,
                    isError ? MessageBoxImage.Error : MessageBoxImage.Information);
            }
        }

        private void AlwaysOnTopCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.Topmost = AlwaysOnTopCheckBox.IsChecked == true;
            if (!_isLoadingProfile) SetProfileDirty(true);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _totalClicksSent = 0;
            ClickCountTextBlock.Text = "0";
        }

        private void ListWindowsButton_Click(object sender, RoutedEventArgs e)
        {
            string allTitles = GetAllVisibleWindowTitles();
            var watermarkWindow = new WatermarkWindow("Visible Windows", allTitles)
            {
                Owner = this
            };
            watermarkWindow.Show();
        }

        private string GetAllVisibleWindowTitles()
        {
            var titles = new List<string>();
            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (IsWindow(hWnd) && IsWindowVisible(hWnd))
                {
                    int len = GetWindowTextLength(hWnd);
                    if (len > 0)
                    {
                        StringBuilder sb = new StringBuilder(len + 1);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        titles.Add(sb.ToString());
                    }
                }
                return true;
            }, IntPtr.Zero);

            titles.Sort();
            return string.Join("\n", titles);
        }
    }
}
