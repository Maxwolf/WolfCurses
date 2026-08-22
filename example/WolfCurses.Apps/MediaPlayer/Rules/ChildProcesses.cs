// Created by Maxwolf (bigmaxwolf.com)
// Timestamp 08/21/2026

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WolfCurses.Apps.MediaPlayer
{
    /// <summary>
    ///     Makes sure the programs this screen starts do not outlive the program that started them.
    ///     <para>
    ///         <b>This exists because it happened.</b> A four-minute trailer was opened, the console window was
    ///         closed with its X button, and ffplay carried on playing it to an empty desktop with nothing left on
    ///         screen to stop it from. Every orderly way out was already covered - ESC clears the form, quitting
    ///         tears the simulation down, and both reach <c>IForm.OnFormClosing</c> - and none of them runs when
    ///         the process is simply killed.
    ///     </para>
    ///     <para>
    ///         So there are two layers, and the second is the one that actually saves you. The managed hooks
    ///         (<see cref="AppDomain.ProcessExit" /> and <see cref="AppDomain.UnhandledException" />) are the same
    ///         pair the library uses to put a console's mouse mode back, and they cover an orderly exit and a crash.
    ///         <b>A Windows job object covers everything else</b>, including the X button, a debugger being
    ///         stopped and a kill from the task manager: children are put in a job marked kill-on-close, and when
    ///         the last handle to that job goes - which happens when this process dies, however it dies - the
    ///         operating system ends them. Nothing has to run in a dying process for that to work, which is the
    ///         whole point, since a dying process is precisely where nothing runs.
    ///     </para>
    ///     <para>
    ///         Everything here fails soft. A job object that cannot be created, or a process that cannot be
    ///         assigned to one, leaves the managed hooks doing what they can; the alternative is a media player
    ///         that refuses to play anything because of a housekeeping detail.
    ///     </para>
    /// </summary>
    internal static class ChildProcesses
    {
        /// <summary>Tells <c>SetInformationJobObject</c> which shape of limits it is being handed.</summary>
        private const int ExtendedLimitInformation = 9;

        /// <summary>End every process in this job when the last handle to it closes.</summary>
        private const uint KillOnJobClose = 0x2000;

        /// <summary>What has been started and might still need ending.</summary>
        private static readonly List<Process> _live = new();

        /// <summary>The job every child is put into, or zero where there is none to be had.</summary>
        private static IntPtr _job = IntPtr.Zero;

        /// <summary>Whether the exit hooks have been attached.</summary>
        private static bool _hooked;

        /// <summary>
        ///     Takes responsibility for a process that has just been started.
        /// </summary>
        /// <param name="process">The process, or null.</param>
        /// <returns>The same process, so this can wrap a call that starts one.</returns>
        public static Process Adopt(Process process)
        {
            if (process == null)
                return null;

            lock (_live)
            {
                Hook();

                // Anything that has already finished is dropped here rather than on a timer, which keeps the list
                // the length of what is actually running: three, at the very most.
                _live.RemoveAll(HasExited);
                _live.Add(process);

                Assign(process);
            }

            return process;
        }

        /// <summary>Gives up responsibility for a process the caller has dealt with itself.</summary>
        /// <param name="process">The process, or null.</param>
        public static void Release(Process process)
        {
            if (process == null)
                return;

            lock (_live)
                _live.Remove(process);
        }

        /// <summary>
        ///     Ends everything still running. Safe to call more than once and safe to call with nothing running,
        ///     which matters because it is reached from two different hooks that may both fire.
        /// </summary>
        public static void KillAll()
        {
            lock (_live)
            {
                foreach (var process in _live)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(true);
                    }
                    catch (Exception exception) when (exception is InvalidOperationException
                                                          or NotSupportedException
                                                          or System.ComponentModel.Win32Exception)
                    {
                        // Already gone, which is the ordinary case by the time anybody asks.
                    }
                }

                _live.Clear();
            }
        }

        /// <summary>Attaches the exit hooks and creates the job, once.</summary>
        private static void Hook()
        {
            if (_hooked)
                return;

            _hooked = true;

            // Both, and not just the first: ProcessExit does not run when an exception escapes the program.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => KillAll();

            _job = CreateJob();
        }

        /// <summary>Creates the kill-on-close job, or gives back zero where there is not one to be had.</summary>
        /// <returns>The job handle, or zero.</returns>
        private static IntPtr CreateJob()
        {
            if (!OperatingSystem.IsWindows())
                return IntPtr.Zero;

            try
            {
                var job = CreateJobObjectW(IntPtr.Zero, null);

                if (job == IntPtr.Zero)
                    return IntPtr.Zero;

                var limits = new ExtendedLimits
                {
                    Basic = new BasicLimits {LimitFlags = KillOnJobClose}
                };

                var size = Marshal.SizeOf<ExtendedLimits>();
                var buffer = Marshal.AllocHGlobal(size);

                try
                {
                    Marshal.StructureToPtr(limits, buffer, false);

                    if (!SetInformationJobObject(job, ExtendedLimitInformation, buffer, (uint) size))
                    {
                        CloseHandle(job);
                        return IntPtr.Zero;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                // Deliberately never closed. The handle dying is exactly what ends the children, so it has to live
                // as long as this process does and no longer.
                return job;
            }
            catch (Exception exception) when (exception is DllNotFoundException
                                                  or EntryPointNotFoundException
                                                  or BadImageFormatException)
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>Puts a process in the job, and shrugs when it cannot.</summary>
        /// <param name="process">The process.</param>
        private static void Assign(Process process)
        {
            if (_job == IntPtr.Zero)
                return;

            try
            {
                AssignProcessToJobObject(_job, process.Handle);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                                                  or NotSupportedException
                                                  or System.ComponentModel.Win32Exception
                                                  or DllNotFoundException)
            {
                // A process that has already exited has no handle to assign, which is the common way here.
            }
        }

        /// <summary>Whether a process has finished, treating "cannot tell" as finished.</summary>
        /// <param name="process">The process.</param>
        /// <returns>TRUE when it is over.</returns>
        private static bool HasExited(Process process)
        {
            try
            {
                return process.HasExited;
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
            {
                return true;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObjectW(IntPtr security, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        ///     <c>JOBOBJECT_BASIC_LIMIT_INFORMATION</c>. The pointer-sized fields are <see cref="UIntPtr" /> rather
        ///     than <c>uint</c> deliberately: they are <c>SIZE_T</c> and <c>ULONG_PTR</c>, so a struct built from
        ///     fixed-width integers is the right size on a 32-bit process and four bytes short per field on a
        ///     64-bit one, which puts every field after the first of them at the wrong offset.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        /// <summary><c>IO_COUNTERS</c>, which is only here because the extended limits contain one.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        /// <summary><c>JOBOBJECT_EXTENDED_LIMIT_INFORMATION</c>, which is what the kill-on-close flag lives in.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
