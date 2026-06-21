using NLog;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Gobchat.Memory.Window
{
    // based on https://stackoverflow.com/questions/4372055/detect-active-window-changed-using-c-sharp-without-polling
    public sealed class WindowObserver : IDisposable
    {
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private const uint WINEVENT_OUTOFCONTEXT = 0x00;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x03;

        // idObject value identifying the window itself (rather than a child UI object); the foreground
        // event only concerns the window, so anything else is ignored.
        private const int OBJID_WINDOW = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);

        public enum EventTypeEnum
        {
            Unknown,
            Foreground
        }

        public event EventHandler<ActiveWindowChangedEventArgs>? ActiveWindowChangedEvent;

        private WinEventDelegate? _wineventDelegate = null;

        private IntPtr _foregroundHook;

        private readonly int _initializedThread;

        public bool Enabled { get; private set; }

        public WindowObserver()
        {
            // set & unset need to be called by the same thread, preferable any thread with a running message pump
            _initializedThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        public void StartObserving()
        {
            if (Enabled)
                return;

            if (_initializedThread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException($"Invalid thread access. Expected thread {_initializedThread} but was {System.Threading.Thread.CurrentThread.ManagedThreadId}");

            _wineventDelegate = new WinEventDelegate(WinEventProc);

            // The foreground event fires whenever the active window changes. This is what "hide while
            // FFXIV isn't focused" needs: in borderless-windowed mode FFXIV is never truly minimized
            // (clicking away just changes the foreground window), so minimize events would never fire.
            _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _wineventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
            if (IntPtr.Zero == _foregroundHook)
                logger.Error("Unable to register window foreground hook!");

            Enabled = true;
        }

        public void StopObserving()
        {
            if (!Enabled)
                return;

            if (_initializedThread != System.Threading.Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException($"Invalid thread access. Expected thread {_initializedThread} but was {System.Threading.Thread.CurrentThread.ManagedThreadId}");

            if (IntPtr.Zero != _foregroundHook)
            {
                UnhookWinEvent(_foregroundHook);
                _foregroundHook = IntPtr.Zero;
            }

            Enabled = false;
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd == IntPtr.Zero)
                return;

            // The foreground event only ever concerns the window object itself; skip child-object
            // notifications (caret, cursor, ...) that share the same event id.
            if (idObject != OBJID_WINDOW)
                return;

            EventTypeEnum evtType = EventTypeEnum.Unknown;

            switch (eventType)
            {
                case EVENT_SYSTEM_FOREGROUND:
                    evtType = EventTypeEnum.Foreground;
                    break;
            }

            if (evtType == EventTypeEnum.Unknown)
                return;

            var windowName = GetWindowTitle(hwnd);
            GetWindowThreadProcessId(hwnd, out var processId);
            ActiveWindowChangedEvent?.Invoke(this, new ActiveWindowChangedEventArgs(processId, windowName, evtType));
        }

        public string? GetActiveWindowTitle()
        {
            return GetWindowTitle(GetForegroundWindow());
        }

        private string? GetWindowTitle(IntPtr hwnd)
        {
            const int nChars = 256;
            StringBuilder buffer = new StringBuilder(nChars);

            if (GetWindowText(hwnd, buffer, nChars) > 0)
            {
                return buffer.ToString();
            }
            return null;
        }

        private string GetProcessName(int pId)
        {
            var processName = "";
            try
            {
                using (var process = System.Diagnostics.Process.GetProcessById(pId))
                {
                    processName = process.ProcessName;
                }
            }
            catch (Exception)
            {
                //already dead
            }
            return processName;
        }

        public void Dispose()
        {
            StopObserving();
        }

        public sealed class ActiveWindowChangedEventArgs : EventArgs
        {
            public string? WindowName { get; }

            public uint ProcessId { get; }

            public EventTypeEnum EventType { get; }

            public ActiveWindowChangedEventArgs(uint processId, EventTypeEnum eventType)
            {
                ProcessId = processId;
                EventType = eventType;
            }

            public ActiveWindowChangedEventArgs(uint processId, string? windowName, EventTypeEnum eventType)
            {
                ProcessId = processId;
                WindowName = windowName;
                EventType = eventType;
            }

            public override string ToString()
            {
                return $"ActiveWindowChangedEventArgs[ProcessId={ProcessId}, WindowName={WindowName}, EventType={EventType}]";
            }
        }
    }
}