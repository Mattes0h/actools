using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FirstFloor.ModernUI.Helpers;

namespace FirstFloor.ModernUI.Windows {
    public enum TaskbarState {
        NoProgress = 0,
        Indeterminate = 0x1,
        Normal = 0x2,
        Error = 0x4,
        Paused = 0x8
    }

    internal class TaskbarProgress : IDisposable {
        private readonly IntPtr _windowHandle;
        private TaskbarState _state;

        public TaskbarProgress() : this(Application.Current?.MainWindow) {
            // If Application.Current == null, it will cause a crash
        }

        public TaskbarProgress(Window window) {
            _windowHandle = new WindowInteropHelper(window).Handle;
        }

        public TaskbarProgress(IntPtr windowHandle) {
            _windowHandle = windowHandle;
        }

        public void Set(TaskbarState state) {
            if (Equals(state, _state)) return;
            _state = state;
            SetState(_windowHandle, state);
        }

        public void Set(double value) {
            SetValue(_windowHandle, (ulong)(value < 0d ? 0d : value > 1d ? 1d : value * 100000d), 100000UL);
        }

        public void Set(long value, long max) {
            SetValue(_windowHandle, (ulong)value, (ulong)max);
        }

        public void Clear() {
            Set(TaskbarState.NoProgress);
        }

        public void Dispose() {
            if (_state != TaskbarState.NoProgress) {
                Clear();
            }
        }

        [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3 {
            // ITaskbarList
            [PreserveSig]
            void HrInit();

            [PreserveSig]
            void AddTab(IntPtr hwnd);

            [PreserveSig]
            void DeleteTab(IntPtr hwnd);

            [PreserveSig]
            void ActivateTab(IntPtr hwnd);

            [PreserveSig]
            void SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            [PreserveSig]
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ITaskbarList3
            [PreserveSig]
            void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);

            [PreserveSig]
            void SetProgressState(IntPtr hwnd, TaskbarState state);
        }

        [Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None), ComImport]
        private class TaskbarInstance { }

        private static ITaskbarList3 _instance;
        private static bool _supported = false; // Environment.OSVersion.Version >= new Version(6, 1);

        private static void SetState(IntPtr handle, TaskbarState state) {
            if (!_supported) return;
            try {
                SetStateUnsafe(handle, state);
            } catch (Exception e) {
                Logging.Warning(e);
                _supported = false;
            }
        }

        private static void SetValue(IntPtr handle, ulong value, ulong max) {
            if (!_supported) return;
            try {
                SetValueUnsafe(handle, value, max);
            } catch (Exception e) {
                Logging.Warning(e);
                _supported = false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetStateUnsafe(IntPtr handle, TaskbarState state) {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (_instance == null) _instance = new TaskbarInstance() as ITaskbarList3;
            _instance?.SetProgressState(handle, state);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetValueUnsafe(IntPtr handle, ulong value, ulong max) {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (_instance == null) _instance = new TaskbarInstance() as ITaskbarList3;
            _instance?.SetProgressValue(handle, value, max);
        }
    }
}