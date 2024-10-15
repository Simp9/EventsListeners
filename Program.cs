using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TestEventsListeners {
    class Interceptor : Form {
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_LBUTTONDOWN = 0x0201;
        private static int WM_QUERYENDSESSION = 0x0011;
        private static bool systemShutdown = false;
        private static LowLevelProc _proc = KeyboardHookCallback;
        private static LowLevelProc _mouseProc = LowLevelMouseProcCallback;
        private static IntPtr _keyboardHookID = IntPtr.Zero;
        private static IntPtr _mouseHookID = IntPtr.Zero;
        private static int numberOfClicks = 0;
        private static int numberOfKeysPressed = 0; 
        private const string clicksTemplate = "Number of left clicks on ";
        private const string keysPressedTemplate = "Number of keys pressed on ";
        private static bool isSameDaySession = false;
        private const string CONTERSFILENAME = "Counters.txt";

        public Interceptor() {
            this.StartPosition = FormStartPosition.Manual;
            this.Width = 0;
            this.Height = 0;
            this.Left = -500;
            this.Top = -500;
        }

        public static void GetInitialData()
        {
            try
            {
                string clicks = string.Empty;
                string keysPressed = string.Empty;

                (clicks, keysPressed) = GetLastTwoLines();

                if (clicks.ToLower().Contains("click"))
                {
                    if (clicks.Substring(clicks.IndexOf(clicksTemplate) + clicksTemplate.Length
                            , clicks.IndexOf(":") - clicksTemplate.Length) ==
                        DateTime.Today.ToString("dd.MM.yyyy"))
                    {
                        numberOfClicks = Int32.Parse(clicks.Split(':').Last());
                        isSameDaySession = true;
                    }
                }
                else
                {
                    throw new Exception("File not well-formed");
                }

                if (keysPressed.ToLower().Contains("key"))
                {
                    if (keysPressed.Substring(keysPressed.IndexOf(keysPressedTemplate) + keysPressedTemplate.Length
                            , keysPressed.IndexOf(":") - keysPressedTemplate.Length) ==
                        DateTime.Today.ToString("dd.MM.yyyy"))
                    {
                        numberOfKeysPressed = Int32.Parse(keysPressed.Split(':').Last());
                        isSameDaySession = true;
                    }
                }
                else
                {
                    throw new Exception("File not well-formed");
                }
            }
            catch (FileNotFoundException e)
            {
                WriteCounters();
                GetInitialData();
            }
        }

        public static void SetHooks()
        {
            _keyboardHookID = SetKeyboardHook(_proc);
            _mouseHookID = SetMouseHook(_mouseProc);
        }

        private static (string, string) GetLastTwoLines()
        {
            StreamReader streamReader = new StreamReader(CONTERSFILENAME);

            string[] lines = streamReader.ReadToEnd().Split('\n');
            string clicks = string.Empty;
            string keysPressed = string.Empty;

            if(lines.Length >= 2)
            {
                if (lines[lines.Length - 1].Length == 0)
                {
                    clicks = lines[lines.Length - 3];
                    keysPressed = lines[lines.Length - 2];
                }
                else
                {
                    clicks = lines[lines.Length - 2];
                    keysPressed = lines[lines.Length - 1];
                }

            }

            streamReader.Close();

            return (clicks, keysPressed);
        }

        private static void WriteCounters(int numberOfClicks = 0, int numberOfKeysPressed = 0)
        {
            StreamWriter streamWriter = new StreamWriter(CONTERSFILENAME);

            streamWriter.WriteLine(clicksTemplate + DateTime.Today.ToString("dd.MM.yyyy") + ":" + numberOfClicks.ToString());
            streamWriter.WriteLine(keysPressedTemplate + DateTime.Today.ToString("dd.MM.yyyy") + ":" + numberOfKeysPressed.ToString());
            streamWriter.Close();
        }

        private static void DeleteLastLines()
        {
            StreamReader streamReader = new StreamReader(CONTERSFILENAME);
            string[] lines = streamReader.ReadToEnd().Trim('\n').Split('\n');

            streamReader.Close();

            File.Delete(CONTERSFILENAME);
            File.Create(CONTERSFILENAME).Close();

            StreamWriter streamWriter = new StreamWriter(CONTERSFILENAME);

            for(int i = 0;i < lines.Length - 2; ++ i)
            {
                streamWriter.WriteLine(lines[i]);
            }

            streamWriter.Close();
        }

        private static void WriteNewCounters()
        {
            if(isSameDaySession == false)
            {
                WriteCounters(numberOfClicks, numberOfKeysPressed);
            } else
            {
                string clicks = string.Empty;
                string keysPressed = string.Empty;

                (clicks, keysPressed) = GetLastTwoLines();
                DeleteLastLines();
                WriteCounters(numberOfClicks, numberOfKeysPressed);
            }
        }

        private static IntPtr SetKeyboardHook(LowLevelProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr SetMouseHook(LowLevelProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelProc (
            int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr KeyboardHookCallback(
            int nCode, IntPtr wParam, IntPtr lParam)
        {
           if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                ++numberOfKeysPressed;
            }
            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        private static IntPtr LowLevelMouseProcCallback(
            int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                ++numberOfClicks;
            }
            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x11) // WM_QUERYENDSESSION 
            {
                WriteNewCounters();
                this.Close();
            }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
                Interceptor.GetInitialData();
                Interceptor.SetHooks();
                Application.Run(new Interceptor());
            } catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }
}