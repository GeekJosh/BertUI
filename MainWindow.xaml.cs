using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace BertUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private delegate void PollGamepadDelegate(object sender, ElapsedEventArgs e);
        bool disposed = false;

        private List<SoundPlayer> sfx = new List<SoundPlayer>();
        private string snd_nav = @"Resources\nav.wav";
        private string snd_click = @"Resources\click.wav";

        private const double BUTTON_LARGE_PCT = 20;
        private const double BUTTON_SMALL_PCT = 75;
        private const double BUTTON_SPACE_PCT = 0.5;

        private const string LAUNCHER_CFG = "launchers.csv";

        double BUTTON_LARGE;
        double BUTTON_SMALL;
        double BUTTON_SPACE;

        Timer gpTimer = new Timer();
        GamepadState gpMonitor = new GamepadState(SlimDX.XInput.UserIndex.One);
        bool haptic = true;

        List<Launcher> launchers = new List<Launcher>();

        public MainWindow()
        {
            InitializeComponent();

            sfx.Add(new SoundPlayer(snd_click));
            sfx.Add(new SoundPlayer(snd_nav));

            try
            {
                sfx.Single(s => s.SoundLocation == snd_click).Load();
            }
            catch (Exception ex)
            {
                logError(ex.Message, EventLogEntryType.Error);
            }

            try
            {
                sfx.Single(s => s.SoundLocation == snd_nav).Load();
            }
            catch (Exception ex)
            {
                logError(ex.Message, EventLogEntryType.Error);
            }

            if (FileSystem.FileExists(LAUNCHER_CFG))
            {
                using (TextFieldParser parser = new TextFieldParser(LAUNCHER_CFG))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");
                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        if (fields.Length == 4)
                        {
                            launchers.Add(new Launcher(
                                fields[0].StartsWith("#", StringComparison.Ordinal) ? "" : fields[0], 
                                fields[1], 
                                fields[2], 
                                fields[3]
                                ));
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show(string.Format(localized.ERROR_LAUNCHER_CONFIG, LAUNCHER_CFG), localized.ERROR, MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BUTTON_LARGE = (MainWin.ColumnDefinitions[1].ActualWidth / 100) * BUTTON_LARGE_PCT;
            BUTTON_SMALL = BUTTON_LARGE * (BUTTON_SMALL_PCT / 100);
            BUTTON_SPACE = BUTTON_LARGE * (BUTTON_SPACE_PCT / 100);

            int tilesWide = (int)(MainWin.ColumnDefinitions[1].ActualWidth / (BUTTON_LARGE + BUTTON_SPACE));
            int tilesHigh = (int)(launchers.Count / tilesWide) + (launchers.Count % tilesWide);

            int x, y;

            for (x = 0; x < tilesWide && x < launchers.Count; x++)
            {
                ColumnDefinition col = new ColumnDefinition();
                col.Width = new GridLength(BUTTON_LARGE + BUTTON_SPACE);
                LauncherGrid.ColumnDefinitions.Add(col);
            }

            for (y = 0; y < tilesHigh && launchers.Count > tilesWide; y++)
            {
                RowDefinition row = new RowDefinition();
                row.Height = new GridLength(BUTTON_LARGE + BUTTON_SPACE);
                LauncherGrid.RowDefinitions.Add(row);
            }

            x = y = 0;
            
            foreach (Launcher launcher in launchers)
            {
                Button btn = new Button();
                btn.Content = launcher.Name;
                launcher.Width = btn.Width = launcher.Height = btn.Height = BUTTON_SMALL;
                btn.SetResourceReference(Button.StyleProperty, "LaunchButton");
                btn.Tag = launcher;
                if (launcher.ImagePath != null && FileSystem.FileExists(launcher.ImagePath))
                {
                    btn.Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(launcher.ImagePath, UriKind.RelativeOrAbsolute))
                    };
                }
                else
                {
                    try
                    {
                        System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(launcher.LaunchPath);
                        btn.Background = new ImageBrush
                        {
                            ImageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                new Int32Rect(0, 0, icon.Width, icon.Height),
                                BitmapSizeOptions.FromEmptyOptions()
                            )
                        };
                    }
                    catch
                    {
                        string err = string.Format(localized.ERROR_LAUNCHER_ICON, launcher.LaunchPath);
                        logError(err, EventLogEntryType.Error);
                        showError(err);
                    }
                }

                if (x == tilesWide)
                {
                    x = 0;
                    y++;
                }

                btn.SetValue(Grid.RowProperty, y);
                btn.SetValue(Grid.ColumnProperty, x);
                LauncherGrid.Children.Add(btn);
                x++;

                btn.MouseEnter += new MouseEventHandler(buttons_MouseEnter);
                btn.GotFocus += new RoutedEventHandler(buttons_GotFocus);
                btn.LostFocus += new RoutedEventHandler(buttons_LostFocus);
                btn.Click += new RoutedEventHandler(buttons_Click);
            }

            if (LauncherGrid.Children.Count > 0)
            {
                LauncherGrid.Children[0].Focus();
            }

            Launcher pbL = new Launcher(localized.POWER_OFF, "shutdown", "/s /t 0");
            pbL.Width = PowerButton.Width;
            pbL.Height = PowerButton.Height;
            PowerButton.Tag = pbL;
            PowerButton.Width = PowerButton.Height = BUTTON_SMALL / 4;
            PowerButton.MouseEnter += new MouseEventHandler(buttons_MouseEnter);
            PowerButton.GotFocus += new RoutedEventHandler(buttons_GotFocus);
            PowerButton.LostFocus += new RoutedEventHandler(buttons_LostFocus);
            PowerButton.Click += new RoutedEventHandler(buttons_Click);

            gpTimer.AutoReset = true;
            gpTimer.Interval = 10;
            gpTimer.Elapsed += PollGamepad;
            gpTimer.Start();
            
        }

        private void PollGamepad(object sender, ElapsedEventArgs e)
        {
            if(Dispatcher.CheckAccess())
            {
                GamepadState current = new GamepadState(SlimDX.XInput.UserIndex.One);
                current.Update();
                if (!current.Equals(gpMonitor))
                {
                    RoutedEvent routedEvent = Keyboard.KeyDownEvent;
                    Key key = Key.None;

                    if (current.DPad.Left) key = Key.Left;
                    if (current.DPad.Right) key = Key.Right;
                    if (current.DPad.Up) key = Key.Up;
                    if (current.DPad.Down) key = Key.Down;
                    if (current.A || current.Start) key = Key.Enter;

                    if (key != Key.None)
                    {
                        if (Keyboard.PrimaryDevice.ActiveSource != null)
                        {
                            InputManager.Current.ProcessInput(
                            new KeyEventArgs(
                                Keyboard.PrimaryDevice,
                                Keyboard.PrimaryDevice.ActiveSource,
                                0,
                                key
                                ) { RoutedEvent = routedEvent }
                            );
                        }

                        if (haptic) current.Vibrate(1f, 1f, 100);
                    }

                    gpMonitor = current;
                }
            }
            else
            {
                Dispatcher.BeginInvoke(
                    new PollGamepadDelegate(PollGamepad), 
                    new object[] { sender, e }
                );
            }
            
        }

        private void btn_ToLarge(Button btn)
        {
            btn.Focus();
            Launcher launcher = (Launcher)btn.Tag;
            DoubleAnimation anim = AnimButtonSize(btn.Width, launcher.Width * 1.2);
            btn.BeginAnimation(Button.WidthProperty, anim);
            btn.BeginAnimation(Button.HeightProperty, anim);
        }

        private void btn_ToSmall(Button btn)
        {
            Launcher launcher = (Launcher)btn.Tag;
            DoubleAnimation anim = AnimButtonSize(btn.Width, launcher.Width);
            btn.BeginAnimation(Button.WidthProperty, anim);
            btn.BeginAnimation(Button.HeightProperty, anim);
        }

        private void buttons_MouseEnter(object sender, EventArgs e)
        {
            btn_ToLarge((Button)sender);
        }

        static private DoubleAnimation AnimButtonSize(double FromSize, double TargetSize, int speedMillis = 250, bool reverse = false)
        {
            DoubleAnimation anim = new DoubleAnimation();
            anim.From = FromSize;
            anim.To = TargetSize;
            anim.AutoReverse = reverse;
            anim.Duration = new Duration(TimeSpan.FromMilliseconds(speedMillis));
            return anim;
        }

        private void buttons_GotFocus(object sender, EventArgs e)
        {
            btn_ToLarge((Button)sender);
        }

        private void buttons_LostFocus(object sender, EventArgs e)
        {
            btn_ToSmall((Button)sender);
        }

        public void buttons_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            Launcher launcher = btn.Tag as Launcher;

            sfx.Single(sf => sf.SoundLocation == snd_click).Play();

            DoubleAnimation anim = AnimButtonSize(btn.Width, btn.Width * 0.9, 50, true);
            btn.BeginAnimation(Button.WidthProperty, anim);
            anim.Completed += (s, ea) =>
                {
                    LaunchProcess(launcher);
                };
            btn.BeginAnimation(Button.HeightProperty, anim);
        }

        private void ChangeWindowState(WindowState state)
        {
            Dispatcher.Invoke(new Action(() => WindowState = state));
        }

        private void LaunchProcess(Launcher launcher)
        {
            ProcessStartInfo pInfo = new ProcessStartInfo(launcher.LaunchPath);

            if (!String.IsNullOrEmpty(launcher.Args)) pInfo.Arguments = launcher.Args;
            try
            {
                Process process = new Process();
                process.StartInfo = pInfo;
                process.EnableRaisingEvents = true;
                process.Exited += (s, e) =>
                    {
                        gpTimer.Start();
                        ChangeWindowState(System.Windows.WindowState.Maximized);
                        process.Dispose();
                    };
                
                process.Start();
                gpTimer.Stop();

                ChangeWindowState(System.Windows.WindowState.Minimized);
            }
            catch
            {
                showError(string.Format(localized.ERROR_LAUNCHER_START, launcher.LaunchPath, LAUNCHER_CFG));
            }
        }

        private void showError(string message)
        {
            ErrorLabel.Content = message;
            System.Media.SystemSounds.Exclamation.Play();
            Storyboard sb = (Storyboard)TryFindResource("ErrorFade");
            if (sb != null) sb.Begin();
        }

        static private void logError(string message, EventLogEntryType eventType = EventLogEntryType.Information)
        {
            //TODO: write error logging routine
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                case Key.Down:
                case Key.Left:
                case Key.Right:
                    sfx.Single(s => s.SoundLocation == snd_nav).Play();
                    break;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                foreach (SoundPlayer s in sfx)
                {
                    s.Dispose();
                }

                gpTimer.Dispose();
            }

            disposed = true;
        }

        ~MainWindow()
        {
            Dispose(false);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
                Rescale();
        }

        private void Rescale()
        {
            BUTTON_LARGE = (MainWin.ColumnDefinitions[1].ActualWidth / 100) * BUTTON_LARGE_PCT;
            BUTTON_SMALL = BUTTON_LARGE * (BUTTON_SMALL_PCT / 100);
            BUTTON_SPACE = BUTTON_LARGE * (BUTTON_SPACE_PCT / 100);

            foreach(Button btn in LauncherGrid.Children.OfType<Button>())
            {
                btn.Width = btn.Height = BUTTON_SMALL;
            }

            PowerButton.Width = PowerButton.Height = BUTTON_SMALL / 4;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.GetPosition(win).Y < 10)
            {
                showError(e.GetPosition(win).ToString());
                win.WindowStyle = System.Windows.WindowStyle.ToolWindow;
            }
            else
            {
                win.WindowStyle = System.Windows.WindowStyle.None;
            }
            Debug.Print(e.GetPosition(win).ToString());
        }
    }
}
