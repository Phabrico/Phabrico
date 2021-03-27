using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Phabrico.Miscellaneous
{
    /// <summary>
    /// Class for logging messages
    /// The directory where the logging is written into is:
    ///   %LOCALAPPDATA%\Phabrico
    /// </summary>
    public class Logging
    {
        private static ConsoleColor _defaultConsoleForegroundColor;
        private static ConsoleColor _currentConsoleForegroundColor;

        /// <summary>
        /// Static property which keeps the instance of this Logging class
        /// </summary>
        private static Logging _instance = null;

        /// <summary>
        /// Synchronization object for keeping calls from different threads separately
        /// </summary>
        private static object _synchronizationObject = new object();

        /// <summary>
        /// Current Date (is used to detect if a new day arrived -> NewDay property)
        /// </summary>
        private DateTime CurrentDate = DateTime.MinValue;

        /// <summary>
        /// True if a new day has arrived since the last logging
        /// If True, the RollFile method will be executed
        /// </summary>
        private bool NewDay;

        /// <summary>
        /// FileStream for current logfile
        /// </summary>
        private FileStream fileStream = null;

        /// <summary>
        /// StreamWriter for current logfile
        /// </summary>
        private StreamWriter streamWriter = null;

        /// <summary>
        /// Static property which determines if a RollFile should be executed
        /// </summary>
        private static Logging Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Logging();
                    _defaultConsoleForegroundColor = Console.ForegroundColor;
                    if (_defaultConsoleForegroundColor == ConsoleColor.Red)
                    {
                        _defaultConsoleForegroundColor = ConsoleColor.Gray;
                    }

                    _currentConsoleForegroundColor = _defaultConsoleForegroundColor;
                }

                _instance.NewDay = _instance.CurrentDate.Equals(DateTime.Now.Date) == false;
                _instance.CurrentDate = DateTime.Now.Date;

                return _instance;
            }
        }

        /// <summary>
        /// Closes the current logfile and creates a new logfile.
        /// Old logfiles will be deleted
        /// </summary>
        private void RollFile()
        {
            // close current logfile
            if (streamWriter != null)
            {
                streamWriter.Close();
                fileStream.Close();

                streamWriter.Dispose();
                fileStream.Dispose();
            }

            // create log directory in case it does not exist yet
            string logDirectory;
            if (Environment.UserInteractive)
            {
                // Phabrico is running as process
                logDirectory = string.Format("{0}\\Phabrico", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            }
            else
            {
                // Phabrico is running as service
                logDirectory = string.Format("{0}\\Phabrico", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
            }

            if (Directory.Exists(logDirectory) == false)
            {
                Directory.CreateDirectory(logDirectory);
            }

            // delete old logfiles
            string oldestLogFileToKeep = logDirectory + "\\" + DateTime.Now.AddDays(-5).ToString("yyyy-MM-dd") + ".log";  // keep log files for maximum 5 days
            foreach (string oldLogFileName in Directory.EnumerateFiles(logDirectory, "????-??-??.log", SearchOption.TopDirectoryOnly).ToList())
            {
                if (oldLogFileName.CompareTo(oldestLogFileToKeep) <= 0)
                {
                    File.Delete(oldLogFileName);
                }
            }

            // create new logfile
            string logFileName = string.Format("{0}\\{1}.log", 
                                    logDirectory,
                                    DateTime.Now.ToString("yyyy-MM-dd"));
            fileStream = File.Open(logFileName, FileMode.Append, FileAccess.Write, FileShare.Read);
            streamWriter = new StreamWriter(fileStream);
            streamWriter.AutoFlush = true;

            Logging.WriteInfo(null, "*** {0} ***", VersionInfo.Version);
        }

        /// <summary>
        /// Logs a formatted string
        /// </summary>
        /// <param name="infoMessage"></param>
        /// <param name="foregroundColor"></param>
        /// <param name="args"></param>
        private void WriteLine(string identifier, string infoMessage, ConsoleColor foregroundColor, params object[] args)
        {
            if (identifier == null)
            {
                identifier = "(internal)";
            }

            string formatter = string.Format("{0} {1} [{2:D5}] {3}", 
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"), 
                identifier.PadRight(20).Substring(0, 20),
                Thread.CurrentThread.ManagedThreadId,
                infoMessage);
            string data = string.Format(formatter, args);
            data = data.Replace("\n", "\n" + new string(' ', formatter.Length - infoMessage.Length));

            if (foregroundColor != _currentConsoleForegroundColor)
            {
                Console.ForegroundColor = foregroundColor;
                _currentConsoleForegroundColor = foregroundColor;
            }

            Console.WriteLine(data);
            streamWriter.WriteLine(data);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="args"></param>
        public static void WriteError(string identifier, string errorMessage, params string[] args)
        {
            lock (_synchronizationObject)
            {
                if (Instance.NewDay)
                {
                    Instance.RollFile();
                }

                Instance.WriteLine(identifier, errorMessage, ConsoleColor.Red, args);
            }
        }

        /// <summary>
        /// Logs an exception
        /// </summary>
        /// <param name="e"></param>
        public static void WriteException(string identifier, System.Exception e)
        {
            lock (_synchronizationObject)
            {
                if (Instance.NewDay)
                {
                    Instance.RollFile();
                }

                string exceptionStackTrace = e.StackTrace
                                              .Split('\n')
                                              .FirstOrDefault();

                Instance.WriteLine(identifier, "{0} {1}", ConsoleColor.Red, e.Message, exceptionStackTrace);
            }
        }

        /// <summary>
        /// Writes the exception stack trace to the windows event log.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="exc"></param>
        /// <param name="eventNr"></param>
        public static void WriteExceptionToEventLog(string identifier, string info, System.Exception exc, int eventNr = 487 /*ERROR_INVALID_ADDRESS*/)
        {
            string applicationName = Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location);
            if (!EventLog.SourceExists(applicationName))
            {
                EventLog.CreateEventSource(applicationName, "Application");
            }

            EventLog.WriteEntry(applicationName, info + ": " + exc + "\r\n\r\n" + exc.StackTrace, EventLogEntryType.Error, eventNr);
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="infoMessage"></param>
        /// <param name="args"></param>
        public static void WriteInfo(string identifier, string infoMessage, params string[] args)
        {
            lock (_synchronizationObject)
            {
                if (Instance.NewDay)
                {
                    Instance.RollFile();
                }

                Instance.WriteLine(identifier, infoMessage, _defaultConsoleForegroundColor, args);
            }
        }
    }
}
