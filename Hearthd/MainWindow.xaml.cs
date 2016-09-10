using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Hearthd
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        };
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        };
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public int dwExtraInfo;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        };

        public delegate void WinEventDelegate(IntPtr hWinEventHook, int eventType, IntPtr hwnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime);

        [DllImport("user32.dll")]
        extern static bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);
        [DllImport("user32.dll")]
        extern static void SendInput(int nInputs, ref INPUT pInputs, int cbsize);
        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(int eventMin, int eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, int idProcess, int idThread, int dwFlags);
        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hwnd, int id, int modkey, int key);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        const int MOUSEEVENTF_MOVE = 0x0001;
        const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        const int MOUSEEVENTF_LEFTUP = 0x0004;
        const int WM_HOTKEY = 0x0312;

        class MouseAction
        {
            public bool wait = false;
            public int x;
            public int y;
            public int waitDuration;
            public Stages availStage;
            public MouseAction(bool wait, int x, int y, int waitDuration, Stages availStage)
            {
                this.x = x;
                this.y = y;
                this.wait = wait;
                this.waitDuration = waitDuration;
                this.availStage = availStage;
            }
        };

        string WindowName = "Hearthstone";
        string ClassName = "UnityWndClass";
        int standardClientWidth = 1366;
        int standardClientHeight = 768;
        const int screenWidth = 1920;
        const int screenHeight = 1080;
        int cardX, cardY, minionX, minionY, tauntX, tauntY;
        DispatcherTimer viewTimer = new DispatcherTimer();
        DispatcherTimer clickTimer = new DispatcherTimer();
        int distanceThresh = 10;
        int stageDuration = 0;
        int hotkeyid = 0;
        int searchTime = 0;
        int heroSearchTime = 0;
        IntPtr hWnd = new IntPtr();
        WriteableBitmap origCapture;
        public static RECT selection;
        public static POINT clickPos;
        Queue<MouseAction> MouseActionQueue = new Queue<MouseAction>();
        int waitTicks = 0;
        int idleTicks = 0;
        bool cardAvail = false;
        bool minionAvail = false;
        bool tauntAvail = false;
        bool heroAvail = false;
        bool heroPowerAvail = false;
        bool heroBlocked = false;

        enum Stages
        {
            Unknown, MainMenu, StartingHand, MyTurn, OpponentTurn, PlayMode, All
        };
        enum Actions
        {
            EnterPlayMode, ConfirmStartingHand, Idle, PlayCard, MinionAttack, AttackTaunt, AttackHero, UseHeroPower,
            EndTurn, EnterGame
        };

        Stages currentStage, lastStage;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadPoint(object sender, RoutedEventArgs e)
        {
            ClickHelper clickHelper = new ClickHelper();
            clickHelper.Closed += UpdatePoint;
            clickHelper.Show();
        }

        private void UpdatePoint(object sender, EventArgs e)
        {
            hWnd = FindWindow(ClassName, WindowName);
            if (hWnd == IntPtr.Zero)
            {
                HaltExecution();
                return;
            }
            POINT pt = new POINT();
            ClientToScreen(hWnd, ref pt);
            PointX.Text = (clickPos.x - pt.x).ToString();
            PointY.Text = (clickPos.y - pt.y).ToString();
            if ((clickPos.x - pt.x) >= 0 && (clickPos.x - pt.x) < standardClientWidth && (clickPos.y - pt.y) >= 0 && (clickPos.y - pt.y) < standardClientHeight)
            {
                System.Windows.Media.Color c = GetPixelColor((clickPos.x - pt.x), (clickPos.y - pt.y));
                ColorRDisplay.Text = c.R.ToString();
                ColorGDisplay.Text = c.G.ToString();
                ColorBDisplay.Text = c.B.ToString();
                ColorDisplay.Background = new SolidColorBrush(c);
            }
        }

        System.Windows.Media.Color GetPixelColor(int x, int y)
        {
            if (StateCheck())
            {
                Bitmap scrshot = new Bitmap(standardClientWidth, standardClientHeight);
                POINT pt = new POINT();
                ClientToScreen(hWnd, ref pt);
                using (Graphics g = Graphics.FromImage(scrshot))
                {
                    g.CopyFromScreen(pt.x, pt.y, 0, 0, new System.Drawing.Size(standardClientWidth, standardClientHeight));
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    BitmapImage bmp = new BitmapImage();

                    scrshot.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    ms.Position = 0;
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.EndInit();

                    System.Windows.Media.Color c = new System.Windows.Media.Color();
                    int width = bmp.PixelWidth;
                    int height = bmp.PixelHeight;
                    int stride = bmp.PixelWidth * (bmp.Format.BitsPerPixel / 8);
                    byte[] pixelData = new byte[stride * bmp.PixelHeight];
                    bmp.CopyPixels(pixelData, stride, 0);

                    c.R = pixelData[stride * y + x * (bmp.Format.BitsPerPixel / 8) + 2];
                    c.G = pixelData[stride * y + x * (bmp.Format.BitsPerPixel / 8) + 1];
                    c.B = pixelData[stride * y + x * (bmp.Format.BitsPerPixel / 8)];
                    c.A = 255;

                    return c;
                }
            }

            return new System.Windows.Media.Color();
        }

        bool StateCheck()
        {
            hWnd = FindWindow(ClassName, WindowName);
            if (hWnd == IntPtr.Zero)
            {
                HaltExecution();
                return false;
            }
            RECT clientRect = new RECT();
            GetClientRect(hWnd, ref clientRect);
            int clientWidth = clientRect.right;
            int clientHeight = clientRect.bottom;
            if (clientHeight != standardClientHeight || clientWidth != standardClientWidth)
            {
                ErrorMessage.Content = "Window size changed";
                ErrorMessage.Visibility = Visibility.Visible;
                return false;
            }
            ErrorMessage.Visibility = Visibility.Hidden;
            return true;
        }

        private void LoadSelection(object sender, RoutedEventArgs e)
        {
            CropHelper cropHelper = new CropHelper();
            cropHelper.Closed += UpdateSelection;
            cropHelper.Show();
        }

        private void UpdateSelection(object sender, EventArgs e)
        {
            hWnd = FindWindow(ClassName, WindowName);
            if (hWnd == IntPtr.Zero)
            {
                HaltExecution();
                return;
            }
            POINT pt = new POINT();
            ClientToScreen(hWnd, ref pt);
            RectL.Text = (selection.left - pt.x).ToString();
            RectT.Text = (selection.top - pt.y).ToString();
            RectR.Text = (selection.right - pt.x).ToString();
            RectB.Text = (selection.bottom - pt.y).ToString();
            RectH.Text = (selection.bottom - selection.top + 1).ToString();
            RectW.Text = (selection.right - selection.left + 1).ToString();
        }

        private void CalculateHash(object sender, RoutedEventArgs e)
        {
            if (StateCheck())
            {
                Bitmap scrshot = new Bitmap(standardClientWidth, standardClientHeight);
                POINT pt = new POINT();
                ClientToScreen(hWnd, ref pt);
                using (Graphics g = Graphics.FromImage(scrshot))
                {
                    g.CopyFromScreen(pt.x, pt.y, 0, 0, new System.Drawing.Size(standardClientWidth, standardClientHeight));
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    BitmapImage bmp = new BitmapImage();

                    scrshot.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    ms.Position = 0;
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    if (selection.right >= selection.left && selection.bottom >= selection.top
                        && (selection.left - pt.x) >= 0 && (selection.right - pt.x) >= 0
                        && (selection.top - pt.y) >= 0 && (selection.bottom - pt.y) >= 0
                        && (selection.left - pt.x) < standardClientWidth && (selection.right - pt.x) < standardClientWidth
                        && (selection.top - pt.y) < standardClientHeight && (selection.bottom - pt.y) < standardClientHeight)
                    {
                        BitmapSource cbmp = CropImage(bmp, selection.left - pt.x, selection.top - pt.y, selection.right - pt.x, selection.bottom - pt.y);
                        ulong hash = GetHash(cbmp);
                        HashDisplay.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255));
                        HashDisplay.Text = hash.ToString();
                    }
                    else
                    {
                        HashDisplay.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 205, 53, 53));
                        HashDisplay.Text = "";
                    }
                }
            }
        }

        ulong GetHash(BitmapSource bmp)
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            BitmapSource tbmp = ResizeImage(bmp, 9, 8);
            int stride = tbmp.PixelWidth * (bmp.Format.BitsPerPixel / 8);
            byte[] pixelData = new byte[stride * tbmp.PixelHeight];
            tbmp.CopyPixels(pixelData, stride, 0);
            ulong hash = 0;
            for (int i = 0; i < tbmp.PixelHeight; i++)
            {
                for (int j = 0; j < tbmp.PixelWidth - 1; j++)
                {
                    int bl = 0; int br = 0;
                    for (int k = 0; k < bmp.Format.BitsPerPixel / 8; k++)
                    {
                        bl += (int)pixelData[i * stride + j * (bmp.Format.BitsPerPixel / 8) + k];
                    }
                    for (int k = 0; k < bmp.Format.BitsPerPixel / 8; k++)
                    {
                        br += (int)pixelData[i * stride + (j + 1) * (bmp.Format.BitsPerPixel / 8) + k];
                    }
                    if (bl > br)
                    {
                        hash += 1;
                    }
                    hash = hash << 1;
                }
            }
            return hash;
        }

        BitmapSource ResizeImage(BitmapSource bmp, double px, double py)
        {
            double scaleX = px / bmp.PixelWidth;
            double scaleY = py / bmp.PixelHeight;
            ScaleTransform trans = new ScaleTransform(scaleX, scaleY);
            TransformedBitmap tbmp = new TransformedBitmap(bmp, trans);
            return tbmp;
        }

        BitmapSource CropImage(BitmapSource bmp, int left, int top, int right, int bottom)
        {
            return new CroppedBitmap(bmp, new Int32Rect(left, top, right - left + 1, bottom - top + 1));
        }

        private void BeginView(object sender, RoutedEventArgs e)
        {
            StateIndicator.Content = "Running";
            StateIndicator.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 125, 230, 100));
            viewTimer.Start();
        }

        private void StopView(object sender, RoutedEventArgs e)
        {
            StateIndicator.Content = "Stopped";
            StateIndicator.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 155, 155));
            viewTimer.Stop();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DetectWindow(this, new RoutedEventArgs());

            viewTimer.Interval = new TimeSpan(0, 0, 0, 1);
            viewTimer.Tick += ViewTimerHandler;

            clickTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            clickTimer.Tick += MouseControl;
            clickTimer.Start();

            Random rnd = new Random();
            hotkeyid = rnd.Next(10000, 40000);
            while (RegisterHotKey(new WindowInteropHelper(this).Handle, hotkeyid, 0, 0x73) == false)
            {
                hotkeyid = rnd.Next(10000, 40000);
            }
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(new HwndSourceHook(WndProc));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == hotkeyid)
                {
                    if (viewTimer.IsEnabled)
                    {
                        StopView(this, new RoutedEventArgs());
                    }
                    else
                    {
                        BeginView(this, new RoutedEventArgs());
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void MouseControl(object sender, EventArgs e)
        {
            if (waitTicks > 0)
            {
                waitTicks -= 1;
                return;
            }
            if (MouseActionQueue.Count != 0)
            {
                MouseAction act = MouseActionQueue.Dequeue();
                if (act.wait)
                {
                    waitTicks = act.waitDuration;
                }
                else
                {
                    if (currentStage == act.availStage || act.availStage == Stages.All)
                        MouseClick(act.x, act.y);
                }
            }
        }

        async Task MouseClick(int xpos, int ypos)
        {
            POINT client = new POINT();

            ClientToScreen(hWnd, ref client);

            MOUSEINPUT mi = new MOUSEINPUT();
            INPUT input = new INPUT();

            mi.dx = (xpos + client.x) * (65535 / screenWidth);
            mi.dy = (ypos + client.y) * (65535 / screenHeight);
            mi.mouseData = 0;
            mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTDOWN;
            mi.time = 0; mi.dwExtraInfo = 0;

            input.type = 0;
            input.mi = mi;

            SendInput(1, ref input, Marshal.SizeOf(input));

            mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE | MOUSEEVENTF_LEFTUP;

            input.mi = mi;

            await Task.Delay(80);

            SendInput(1, ref input, Marshal.SizeOf(input));
        }


        private void ViewTimerHandler(object sender, EventArgs e)
        {
            RefreshView();
        }

        private void RefreshView()
        {
            Stopwatch watch = Stopwatch.StartNew();
            if (StateCheck())
            {
                Bitmap scrshot = new Bitmap(standardClientWidth, standardClientHeight);
                POINT pt = new POINT();
                ClientToScreen(hWnd, ref pt);
                using (Graphics g = Graphics.FromImage(scrshot))
                {
                    g.CopyFromScreen(pt.x, pt.y, 0, 0, new System.Drawing.Size(standardClientWidth, standardClientHeight));
                }
                using (MemoryStream ms = new MemoryStream())
                {
                    BitmapImage bmp = new BitmapImage();

                    scrshot.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    ms.Position = 0;
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    origCapture = new WriteableBitmap(bmp);
                }

                lastStage = currentStage;

                DetermineStage();

                RefreshDuration();

                FindCard();
                FindMinion();
                FindTaunt();

                GetHeroAvailability();
                GetHeroPowerAvailability();

                ExecuteScheme();

            }
            else
            {
                StopView(this, new RoutedEventArgs());
            }
            watch.Stop();
            ViewCounter.Content = watch.ElapsedMilliseconds.ToString();
        }

        private void GetHeroPowerAvailability()
        {
            heroPowerAvail = false;
            System.Windows.Media.Color c = GetPixelColor(826, 537);
            if (c.G == 255)
            {
                heroPowerAvail = true;
            }
            else
            {
                heroPowerAvail = false;
            }

            if (heroPowerAvail)
            {
                HeroPowerAvailabilityDisplay.Content = "Available";
            }
            else
            {
                HeroPowerAvailabilityDisplay.Content = "Inavailable";
            }
        }

        private void GetHeroAvailability()
        {
            heroAvail = false;
            System.Windows.Media.Color c = GetPixelColor(743, 125);
            if ((int)c.R + (int)c.G + (int)c.B > 500)
            {
                heroAvail = true;
            }
            else
            {
                heroAvail = false;
            }

            if (heroAvail)
            {
                HeroAvailabilityDisplay.Content = "Available";
            }
            else
            {
                HeroAvailabilityDisplay.Content = "Inavailable";
            }
        }

        private void FindTaunt()
        {
            BitmapSource cbmp = CropImage(origCapture, 400, 288, 900, 288);
            int width = cbmp.PixelWidth;
            int height = cbmp.PixelHeight;
            int stride = cbmp.PixelWidth * (cbmp.Format.BitsPerPixel / 8);
            byte[] pixelData = new byte[stride * cbmp.PixelHeight];
            cbmp.CopyPixels(pixelData, stride, 0);
            tauntAvail = false;
            for (int i = 0; i < cbmp.PixelHeight; i++)
            {
                for (int j = 0; j < cbmp.PixelWidth; j++)
                {
                    if (pixelData[i * stride + j * (cbmp.Format.BitsPerPixel / 8) + 1] == 255 && pixelData[i * stride + j * (cbmp.Format.BitsPerPixel / 8) + 2] == 255 &&
                        ((int)pixelData[j * (cbmp.Format.BitsPerPixel / 8)] + (int)pixelData[j * (cbmp.Format.BitsPerPixel / 8) + 1] + (int)pixelData[j * (cbmp.Format.BitsPerPixel / 8) + 2]) < 700)
                    {
                        tauntX = 400 + j - 10;
                        tauntY = 288;
                        tauntAvail = true;
                    }
                }
            }
            if (tauntAvail)
            {
                TauntAvailabilityDisplay.Content = "Available";
            }
            else
            {
                TauntAvailabilityDisplay.Content = "Inavailable";
            }
        }

        private void FindCard()
        {
            BitmapSource cbmp = CropImage(origCapture, 400, 764, 900, 764);
            int width = cbmp.PixelWidth;
            int height = cbmp.PixelHeight;
            int stride = cbmp.PixelWidth * (cbmp.Format.BitsPerPixel / 8);
            byte[] pixelData = new byte[stride * cbmp.PixelHeight];
            cbmp.CopyPixels(pixelData, stride, 0);
            cardAvail = false;
            for (int i = 0; i < cbmp.PixelHeight; i++)
            {
                for (int j = 0; j < cbmp.PixelWidth; j++)
                {
                    if (pixelData[i * stride + j * (cbmp.Format.BitsPerPixel / 8) + 1] == 255 &&
                        ((int)pixelData[j * (cbmp.Format.BitsPerPixel / 8)] + (int)pixelData[j * (cbmp.Format.BitsPerPixel / 8) + 1] + (int)pixelData[j * (cbmp.Format.BitsPerPixel / 8) + 2]) < 510)
                    {
                        cardX = 400 + j;
                        cardY = 764;
                        cardAvail = true;
                    }
                }
            }
            if (cardAvail)
            {
                CardAvailabilityDisplay.Content = "Available";
            }
            else
            {
                CardAvailabilityDisplay.Content = "Inavailable";
            }
        }

        private void FindMinion()
        {
            BitmapSource cbmp = CropImage(origCapture, 277, 419, 1098, 419);
            int width = cbmp.PixelWidth;
            int height = cbmp.PixelHeight;
            int stride = cbmp.PixelWidth * (cbmp.Format.BitsPerPixel / 8);
            byte[] pixelData = new byte[stride * cbmp.PixelHeight];
            cbmp.CopyPixels(pixelData, stride, 0);
            minionAvail = false;
            for (int i = 0; i < cbmp.PixelHeight; i++)
            {
                for (int j = 0; j < cbmp.PixelWidth; j++)
                {
                    if (pixelData[i * stride + j * (cbmp.Format.BitsPerPixel / 8) + 1] == 255 &&
                        ((int)pixelData[j * (cbmp.Format.BitsPerPixel / 8)] + (int)pixelData[j * (cbmp.Format.BitsPerPixel / 8) + 1] + (int)pixelData[j * (cbmp.Format.BitsPerPixel / 8) + 2]) < 510)
                    {
                        minionX = 277 + j - 10;
                        minionY = 419;
                        minionAvail = true;
                    }
                }
            }
            if (minionAvail)
            {
                MinionAvailabilityDisplay.Content = "Available";
            }
            else
            {
                MinionAvailabilityDisplay.Content = "Inavailable";
            }
        }

        private void ExecuteScheme()
        {
            if (currentStage == Stages.MainMenu)
            {
                TakeAction(Actions.EnterPlayMode);
            }
            else if (currentStage == Stages.StartingHand)
            {
                TakeAction(Actions.ConfirmStartingHand);
            }
            else if (currentStage == Stages.Unknown)
            {
                TakeAction(Actions.Idle);
            }
            else if (currentStage == Stages.PlayMode)
            {
                TakeAction(Actions.EnterGame);
            }
            else if (currentStage == Stages.MyTurn)
            {
                if (cardAvail)
                {
                    idleTicks = 0;
                    TakeAction(Actions.PlayCard);
                }
                else if (minionAvail)
                {
                    idleTicks = 0;
                    searchTime = 0;
                    heroSearchTime = 0;
                    TakeAction(Actions.MinionAttack);
                }
                else if (heroAvail && !heroBlocked)
                {
                    idleTicks = 0;
                    heroSearchTime++;
                    if (heroSearchTime > 7)
                    {
                        heroBlocked = true;
                        heroSearchTime = 0;
                    }
                    TakeAction(Actions.AttackHero);
                }
                else if (tauntAvail)
                {
                    heroBlocked = false;
                    idleTicks = 0;
                    searchTime++;
                    if (searchTime > 7)
                    {
                        if (heroPowerAvail)
                            TakeAction(Actions.UseHeroPower);
                        TakeAction(Actions.EndTurn);
                        searchTime = 0;
                    }
                    else
                    {
                        TakeAction(Actions.AttackTaunt);
                    }
                }
                else
                {
                    heroBlocked = false;
                    idleTicks++;
                    if (idleTicks > 5)
                    {
                        if (heroPowerAvail)
                            TakeAction(Actions.UseHeroPower);
                        TakeAction(Actions.EndTurn);
                        heroBlocked = false;
                        searchTime = 0;
                        heroSearchTime = 0;
                    }
                }

            }
        }

        void TakeAction(Actions action)
        {
            if (action == Actions.EnterPlayMode)
            {
                MouseAction act = new MouseAction(false, 681, 234, 0, Stages.MainMenu);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.ConfirmStartingHand)
            {
                MouseAction act = new MouseAction(false, 682, 607, 0, Stages.StartingHand);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.Idle)
            {
                MouseAction act = new MouseAction(false, 10, 10, 0, Stages.Unknown);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.PlayCard)
            {
                MouseAction act = new MouseAction(false, cardX, cardY, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 683, 428, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 10, 10, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.MinionAttack)
            {
                MouseAction act = new MouseAction(false, minionX, minionY, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 79, 421, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.AttackTaunt)
            {
                MouseAction act = new MouseAction(false, tauntX, tauntY, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 10, 10, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.AttackHero)
            {
                MouseAction act = new MouseAction(false, 687, 144, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 10, 10, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.UseHeroPower)
            {
                MouseAction act = new MouseAction(false, 809, 587, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 10, 10, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.EndTurn)
            {
                MouseAction act = new MouseAction(false, 1104, 349, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 10, 10, 0, Stages.MyTurn);
                MouseActionQueue.Enqueue(act);
            }
            else if (action == Actions.EnterGame)
            {
                MouseAction act = new MouseAction(false, 929, 114, 0, Stages.PlayMode);
                MouseActionQueue.Enqueue(act);
                act = new MouseAction(false, 995, 628, 0, Stages.PlayMode);
                MouseActionQueue.Enqueue(act);
            }
        }

        void RefreshDuration()
        {
            if (lastStage == currentStage)
            {
                stageDuration += 1;
            }
            else
            {
                stageDuration = 0;
            }
            StageDurationDisplay.Content = stageDuration.ToString();
        }

        private void DetermineStage()
        {
            if (GetDistance(GetAreaHash(357, 659, 400, 695), 10000887605555998670) < distanceThresh)
            {
                currentStage = Stages.MainMenu;
                StageDisplay.Content = "Main Menu";
            }
            else if (GetDistance(GetAreaHash(960, 596, 1034, 673), 4496469098953715256) < distanceThresh)
            {
                currentStage = Stages.PlayMode;
                StageDisplay.Content = "Play Mode";
            }
            else if (GetDistance(GetAreaHash(641, 596, 732, 623), 2699223683689460834) < distanceThresh)
            {
                currentStage = Stages.StartingHand;
                StageDisplay.Content = "Starting Hand";
            }
            else if (GetDistance(GetAreaHash(1072, 341, 1149, 363), 7540478887027917266) < distanceThresh ||
                GetDistance(GetAreaHash(1071, 341, 1150, 359), 7918785258118000040) < distanceThresh ||
                GetDistance(GetAreaHash(1065, 343, 1149, 364), 7301911271933560018) < distanceThresh)
            {
                currentStage = Stages.MyTurn;
                StageDisplay.Content = "My Turn";
            }
            else if (GetDistance(GetAreaHash(1073, 344, 1151, 361), 2242749307094146982) < distanceThresh)
            {
                currentStage = Stages.OpponentTurn;
                StageDisplay.Content = "Opponent Turn";
            }
            else
            {
                currentStage = Stages.Unknown;
                StageDisplay.Content = "Unknown";
            }
        }

        int GetDistance(ulong a, ulong b)
        {
            int dist = 0;
            ulong c = a ^ b;
            for (int i = 0; i < 64; i++)
            {
                if ((c & 1) == 1)
                {
                    dist++;
                }
                c = c >> 1;
            }
            return dist;
        }

        ulong GetAreaHash(int left, int top, int right, int bottom)
        {
            ulong hash;
            if (StateCheck())
            {
                BitmapSource cbmp = CropImage(origCapture, left, top, right, bottom);
                hash = GetHash(cbmp);
                return hash;
            }
            return 0;
        }

        private void DetectWindow(object sender, RoutedEventArgs e)
        {
            hWnd = FindWindow(ClassName, WindowName);
            if (hWnd == IntPtr.Zero)
            {
                HaltExecution();
                return;
            }
            BeginExecution();
        }
        void BeginExecution()
        {
            Veil.Visibility = Visibility.Hidden;
            ControlPanel.IsEnabled = true;
        }
        void HaltExecution()
        {
            Veil.Visibility = Visibility.Visible;
            ControlPanel.IsEnabled = false;
        }
    }
}
