using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Globalization;

namespace BackgroundClicker.Wpf
{
    public partial class AutoClickerWindow : Window
    {
        #region Win32 API
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_ID_TOGGLE = 2; 
        private const int HOTKEY_ID_PICK = 3;   
        private const uint MOD_NONE = 0x0000;
        private const uint VK_F9 = 0x78;
        private const uint VK_F10 = 0x79;
        private const int WM_HOTKEY = 0x0312;
        #endregion

        private CancellationTokenSource _clickCts;
        private bool _isRunning;
        private readonly Random _random = new Random();
        private HwndSource _hwndSource;
        private uint _currentToggleVk = VK_F9;

        public AutoClickerWindow()
        {
            InitializeComponent();
            StatusTextBlock.Text = "Idle";
            UpdateRunningState(false);
            SourceInitialized += AutoClickerWindow_SourceInitialized;
            Closed += AutoClickerWindow_Closed;
        }

        private void AutoClickerWindow_SourceInitialized(object sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            if (_hwndSource != null)
            {
                _hwndSource.AddHook(WndProc);

                
                
                if (UseHotkeyCheckBox != null && UseHotkeyCheckBox.IsChecked == true)
                {
                    RegisterHotKeySafe();
                }
            }
        }

        private void AutoClickerWindow_Closed(object sender, EventArgs e)
        {
            StopClicking();
            if (_hwndSource != null)
            {
                UnregisterHotKeySafe();
                _hwndSource.RemoveHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_TOGGLE)
                {
                    ToggleClickingFromHotkey();
                    handled = true;
                }
                else if (id == HOTKEY_ID_PICK)
                {
                    PickCurrentPositionFromHotkey();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void ToggleClickingFromHotkey()
        {
            if (!UseHotkeyCheckBox.IsChecked.GetValueOrDefault())
            {
                return;
            }

            if (!_isRunning)
            {
                StartClicking();
            }
            else
            {
                StopClicking();
            }
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
            {
                StartClicking();
            }
            else
            {
                StopClicking();
            }
        }

        private void StartClicking()
        {
            if (_isRunning)
            {
                return;
            }

            double intervalMs;
            string intervalText = IntervalMsTextBox.Text != null ? IntervalMsTextBox.Text.Trim() : string.Empty;

            
            
            bool parsed = double.TryParse(intervalText, NumberStyles.Float, CultureInfo.InvariantCulture, out intervalMs)
                          || double.TryParse(intervalText, NumberStyles.Float, CultureInfo.CurrentCulture, out intervalMs);

            if (!parsed || intervalMs <= 0)
            {
                StatusTextBlock.Text = "Invalid interval";
                MessageBox.Show(this,
                    "Interval must be a positive number (milliseconds).",
                    "Invalid interval",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            POINT fixedPoint = new POINT();
            bool useFixed = UseFixedPositionRadio.IsChecked == true;
            bool randomize = RandomizeIntervalCheckBox != null && RandomizeIntervalCheckBox.IsChecked == true;

            if (useFixed)
            {
                if (!int.TryParse(FixedXTextBox.Text, out fixedPoint.X) || !int.TryParse(FixedYTextBox.Text, out fixedPoint.Y))
                {
                    StatusTextBlock.Text = "Invalid fixed position";
                    MessageBox.Show(this,
                        "Fixed X and Y must be valid integers.",
                        "Invalid position",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            _clickCts = new CancellationTokenSource();
            var token = _clickCts.Token;

            
            
            ComboBoxItem buttonItem = ButtonTypeComboBox.SelectedItem as ComboBoxItem;
            string button = buttonItem != null ? buttonItem.Content as string : null;
            if (string.IsNullOrEmpty(button))
            {
                button = "Left";
            }

            ComboBoxItem clickTypeItem = ClickTypeComboBox.SelectedItem as ComboBoxItem;
            bool doubleClick = clickTypeItem != null && (clickTypeItem.Content as string) == "Double";

            
            bool showClickCount = ShowClickCountCheckBox.IsChecked == true;

            _isRunning = true;
            UpdateRunningState(true);

            int totalClicks = 0;

            Task.Run(() =>
            {
                var stopwatch = new System.Diagnostics.Stopwatch();
                var uiUpdateStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var frequency = (double)System.Diagnostics.Stopwatch.Frequency;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        stopwatch.Restart();

                        POINT clickPoint;

                        if (useFixed)
                        {
                            clickPoint = fixedPoint;
                        }
                        else
                        {
                            if (!GetCursorPos(out clickPoint))
                            {
                                
                                int fallbackDelayMs = (int)Math.Max(1.0, Math.Round(intervalMs));
                                Thread.Sleep(fallbackDelayMs); 
                                continue;
                            }
                        }

                        PerformClick(clickPoint, button, doubleClick);
                        totalClicks++;
                        
                        
                        if (showClickCount && uiUpdateStopwatch.ElapsedMilliseconds > 100)
                        {
                            UpdateClickCounterSafe(totalClicks, true);
                            uiUpdateStopwatch.Restart();
                        }

                        double effectiveIntervalMs = intervalMs;
                        if (randomize)
                        {
                            double factor = 1.0 + ((_random.NextDouble() * 0.4) - 0.2);
                            effectiveIntervalMs = intervalMs * factor;
                        }

                        
                        
                        long targetTicks = (long)(effectiveIntervalMs / 1000.0 * frequency);

                        
                        while (stopwatch.ElapsedTicks < targetTicks)
                        {
                            if (token.IsCancellationRequested)
                            {
                                break;
                            }
                            
                            
                            Thread.SpinWait(20);
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    
                }
                catch (Exception ex)
                {
                    
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        StatusTextBlock.Text = "Error: " + (ex != null ? ex.Message : "unknown");
                    }));
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        _isRunning = false;
                        UpdateRunningState(false);
                    });
                }
            }, token);
        }

        private void StopClicking()
        {
            if (!_isRunning)
            {
                return;
            }

            try
            {
                if (_clickCts != null)
                {
                    _clickCts.Cancel();
                }
            }
            catch
            {
            }
        }

        private void UpdateClickCounterSafe(int totalClicks, bool showClickCount)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateClickCounterSafe(totalClicks, showClickCount);
                }));
                return;
            }

            if (showClickCount)
            {
                ClickCounterTextBlock.Visibility = Visibility.Visible;
                ClickCounterTextBlock.Text = totalClicks + " clicks";
            }
            else
            {
                ClickCounterTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateRunningState(bool isRunning)
        {
            StartStopButton.Content = isRunning ? "STOP" : "START";
            StartStopButton.Background = isRunning
                ? new SolidColorBrush(Colors.DarkRed)
                : new SolidColorBrush(Color.FromRgb(0x22, 0x8B, 0x22));
            StartStopButton.BorderBrush = isRunning
                ? new SolidColorBrush(Colors.Red)
                : new SolidColorBrush(Color.FromRgb(0x3C, 0xB3, 0x71));

            StatusTextBlock.Text = isRunning ? "Running..." : "Idle";
            IntervalMsTextBox.IsEnabled = !isRunning;
            ButtonTypeComboBox.IsEnabled = !isRunning;
            ClickTypeComboBox.IsEnabled = !isRunning;
            UseCurrentCursorRadio.IsEnabled = !isRunning;
            UseFixedPositionRadio.IsEnabled = !isRunning;
            FixedXTextBox.IsEnabled = !isRunning && UseFixedPositionRadio.IsChecked == true;
            FixedYTextBox.IsEnabled = !isRunning && UseFixedPositionRadio.IsChecked == true;
            PickCurrentPositionButton_IsEnabledUpdate();
            RandomizeIntervalCheckBox.IsEnabled = !isRunning;
            UseHotkeyCheckBox.IsEnabled = !isRunning;
        }

        private void PerformClick(POINT point, string button, bool doubleClick)
        {
            uint downFlag;
            uint upFlag;

            if (string.IsNullOrEmpty(button))
            {
                button = "Left";
            }

            switch (button)
            {
                case "Right":
                    downFlag = MOUSEEVENTF_RIGHTDOWN;
                    upFlag = MOUSEEVENTF_RIGHTUP;
                    break;
                case "Middle":
                    downFlag = MOUSEEVENTF_MIDDLEDOWN;
                    upFlag = MOUSEEVENTF_MIDDLEUP;
                    break;
                default:
                    downFlag = MOUSEEVENTF_LEFTDOWN;
                    upFlag = MOUSEEVENTF_LEFTUP;
                    break;
            }

            SendMouseInput(point.X, point.Y, downFlag, upFlag);
            if (doubleClick)
            {
                SendMouseInput(point.X, point.Y, downFlag, upFlag);
            }
        }

        private static void SendMouseInput(int x, int y, uint downFlag, uint upFlag)
        {
            
            SetCursorPos(x, y);

            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = downFlag;

            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi.dwFlags = upFlag;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UseCurrentCursorRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            FixedXTextBox.IsEnabled = false;
            FixedYTextBox.IsEnabled = false;
            PickCurrentPositionButton_IsEnabledUpdate();
        }

        private void UseFixedPositionRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            FixedXTextBox.IsEnabled = true;
            FixedYTextBox.IsEnabled = true;
            PickCurrentPositionButton_IsEnabledUpdate();
        }

        private void PickCurrentPositionButton_IsEnabledUpdate()
        {
            Button pickButton = this.FindName("PickCurrentPositionButton") as Button;
            if (pickButton != null)
            {
                pickButton.IsEnabled = !_isRunning && UseFixedPositionRadio.IsChecked == true;
            }
        }

        private void PickCurrentPositionButton_Click(object sender, RoutedEventArgs e)
        {
            POINT pos;
            if (GetCursorPos(out pos))
            {
                FixedXTextBox.Text = pos.X.ToString();
                FixedYTextBox.Text = pos.Y.ToString();
            }
        }

        private void PickCurrentPositionFromHotkey()
        {
            
            if (UseFixedPositionRadio != null)
            {
                UseFixedPositionRadio.IsChecked = true;
            }

            
            PickCurrentPositionButton_Click(this, new RoutedEventArgs());
        }

        private void UseHotkeyCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RegisterHotKeySafe();
        }

        private void UseHotkeyCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UnregisterHotKeySafe();
        }

        private void ToggleHotkeyTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            
            if (UseHotkeyCheckBox != null && UseHotkeyCheckBox.IsChecked == true)
            {
                RegisterHotKeySafe();
            }
        }

        private void ShowClickCountCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            
            
            CheckBox source = sender as CheckBox;
            bool isChecked = false;
            if (source != null)
            {
                isChecked = source.IsChecked == true;
            }
            else if (ShowClickCountCheckBox != null)
            {
                isChecked = ShowClickCountCheckBox.IsChecked == true;
            }

            
            if (ClickCounterTextBlock == null)
            {
                return;
            }

            if (isChecked)
            {
                if (ClickCounterTextBlock.Visibility != Visibility.Visible)
                {
                    ClickCounterTextBlock.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ClickCounterTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void RegisterHotKeySafe()
        {
            if (_hwndSource == null) return;
            var handle = _hwndSource.Handle;

            
            string text = "F9";
            if (ToggleHotkeyTextBox != null && ToggleHotkeyTextBox.Text != null)
            {
                text = ToggleHotkeyTextBox.Text.Trim().ToUpperInvariant();
                if (text.Length == 0)
                {
                    text = "F9";
                }
            }

            uint vk;
            if (!TryParseVirtualKey(text, out vk))
            {
                vk = VK_F9;
            }
            _currentToggleVk = vk;

            
            UnregisterHotKey(handle, HOTKEY_ID_TOGGLE);
            UnregisterHotKey(handle, HOTKEY_ID_PICK);

            RegisterHotKey(handle, HOTKEY_ID_TOGGLE, MOD_NONE, _currentToggleVk);
            RegisterHotKey(handle, HOTKEY_ID_PICK, MOD_NONE, VK_F10);
        }

        private void UnregisterHotKeySafe()
        {
            if (_hwndSource == null) return;
            var handle = _hwndSource.Handle;
            UnregisterHotKey(handle, HOTKEY_ID_TOGGLE);
            UnregisterHotKey(handle, HOTKEY_ID_PICK);
        }

        private static bool TryParseVirtualKey(string text, out uint vk)
        {
            vk = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            
            if (text.Length >= 2 && text[0] == 'F')
            {
                int num;
                if (int.TryParse(text.Substring(1), out num) && num >= 1 && num <= 24)
                {
                    vk = (uint)(0x70 + (num - 1)); 
                    return true;
                }
            }

            
            if (text.Length == 1)
            {
                char c = text[0];
                if (c >= 'A' && c <= 'Z')
                {
                    vk = (uint)c; 
                    return true;
                }
                if (c >= '0' && c <= '9')
                {
                    vk = (uint)c; 
                    return true;
                }
            }

            return false;
        }
    }
}
