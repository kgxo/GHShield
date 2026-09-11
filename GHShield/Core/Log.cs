using System;

using Rhino;

namespace GHShield.Core
{
    /// <summary>
    /// All GHShield console output.
    ///
    /// POLICY
    ///
    /// The Rhino command line belongs to the user. GHShield's normal feedback
    /// is visual - the frost and padlock on the canvas, and the counts in the
    /// GHShield panel. A log line that only repeats what the user can already
    /// see is noise.
    ///
    /// Info()  - only when the user did something and NOTHING visible
    ///           happened ("nothing selected"), or when they must be told
    ///           something ("protection loaded from this file").
    /// Debug() - per-object and per-interaction tracing. Off unless verbose
    ///           logging is switched on, and used for diagnosis only.
    /// Error() - genuine failures. Always shown.
    ///
    /// Identical consecutive messages are collapsed, because blocked
    /// interactions arrive in bursts as the mouse moves.
    /// </summary>
    public static class Log
    {
        public static bool Verbose = false;

        private const double SuppressWindowMs = 1500.0;

        private static string _lastMessage;
        private static DateTime _lastTime = DateTime.MinValue;

        private static bool IsRepeat(string key)
        {
            DateTime now = DateTime.UtcNow;

            if (key == _lastMessage &&
                (now - _lastTime).TotalMilliseconds < SuppressWindowMs)
            {
                return true;
            }

            _lastMessage = key;
            _lastTime = now;

            return false;
        }

        public static void Info(string message)
        {
            if (IsRepeat("I:" + message))
                return;

            RhinoApp.WriteLine("GHShield: " + message);
        }

        public static void Debug(string message)
        {
            if (!Verbose)
                return;

            if (IsRepeat("D:" + message))
                return;

            RhinoApp.WriteLine("GHShield [dbg]: " + message);
        }

        public static void Error(string message)
        {
            RhinoApp.WriteLine("GHShield ERROR: " + message);
        }
    }
}
