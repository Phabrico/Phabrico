using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

using Phabrico.Miscellaneous;

namespace Phabrico
{
    /// <summary>
    /// Represents a Windows Service
    /// </summary>
    [DesignerCategory("")]
    public class WindowsService : ServiceBase
    {
        /// <summary>
        /// Internal Name of the application
        /// </summary>
        public const string Name = "Phabrico";

        /// <summary>
        /// Name of the application
        /// </summary>
        public const string DisplayName = "Phabrico";

        /// <summary>
        /// Description of the application
        /// </summary>
        public const string Description = "Offline Reader and Editor for Phabricator documents and tasks";

        private static Mutex singleInstanceMutex = new Mutex(false, MutexName);
        private static Task serviceMainTask = null;
        private static TaskStatus serviceMainTaskStatus = TaskStatus.WaitingToRun; 
        private static CancellationTokenSource serviceMainTaskCancellationTokenSource;
        private static bool escapeKeyPressed = false; 

        /// <summary>
        /// Mutex which monitors that the application can not be executed multiple times at the same time
        /// </summary>
        private static string MutexName
        {
            get
            {
                string mutexName = "Global\\Phabrico";
                string mutexNameSeed = Path.GetFullPath(Storage.Database.DataSource);
                mutexNameSeed = mutexNameSeed.Replace("\\Phabrico.data", "").ToUpper(CultureInfo.InvariantCulture);
                mutexNameSeed = RegexSafe.Replace(mutexNameSeed, "[^A-Z0-9]", "");
                
                if (mutexNameSeed.Length + mutexName.Length > 240)
                {
                    int maxMutexNameSeedLength = 240 - mutexName.Length;

                    mutexNameSeed = mutexNameSeed.Substring(0, maxMutexNameSeedLength / 2)
                                  + mutexNameSeed.Substring(mutexNameSeed.Length - maxMutexNameSeedLength / 2);
                }

                return mutexName + mutexNameSeed;
            }
        }

        /// <summary>
        /// True if the service application thread is still running
        /// </summary>
        public static bool Running
        {
            get
            {
                if (Environment.UserInteractive)
                {
                   if (escapeKeyPressed == false) 
                   { 
                       escapeKeyPressed = Console.KeyAvailable == true && 
                                          Console.ReadKey(true).Key == ConsoleKey.Escape;
                   } 
 
                   return escapeKeyPressed == false;  
                }
                else
                {
                    return serviceMainTaskCancellationTokenSource.Token.IsCancellationRequested == false;
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public WindowsService()
        {
            base.ServiceName = WindowsService.Name;
            this.EventLog.Log = "Application";
        }

        /// <summary>
        /// The Main
        /// </summary>
        public static void Main()
        {
            Program program = new Program();

            if (Environment.UserInteractive)
            {
                // program is running as process (i.e. non-service)

                // check if we have any command line arguments
                string[] arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
                if (arguments.Any())
                {
                    Commander commander = new Commander(program, arguments);
                    if (commander.Action != Commander.CommanderAction.Nothing)
                    {
                        commander.Execute();
                    }
                }
                else
                {
                    Logging.WriteInfo(null, "*** Startup ***");

                    // only 1 instance of Phabrico is allowed 
                    if (singleInstanceMutex.WaitOne(TimeSpan.Zero, false) == false)
                    {
                        Logging.WriteInfo(null, "ERROR: Phabrico is already running");
                        Environment.Exit(-1);
                    }

                    Logging.WriteInfo(null, "press <Esc> to stop, <Ctrl+C> to abort.");
                    try
                    {
                        program.ServiceMain();

                        Logging.WriteInfo(null, "*** Shutdown ***");
                    }
                    catch (System.Exception e)
                    {
                        Logging.WriteInfo(null, "*** Shutdown by {0} ***", e.ToString());
                        Logging.WriteException(null, e);
                    }
                }
            }
            else
            {
                Logging.WriteInfo(null, "*** Startup ***");

                // program is running  as a service
                ServiceBase.Run(new WindowsService());
            }
        }

        /// <summary>
        /// This method is fired when Phabrico is started as service
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            // only 1 instance of Phabrico is allowed 
            if (singleInstanceMutex.WaitOne(TimeSpan.Zero, false) == false)
            {
                Console.WriteLine("ERROR: Phabrico is already running");
                Environment.Exit(-1);
            }

            serviceMainTaskCancellationTokenSource = new CancellationTokenSource();

            serviceMainTask = Task.Factory.StartNew(() =>
            {
                Program program = new Program();

                try
                {
                    serviceMainTaskStatus = TaskStatus.Running; 
                    program.ServiceMain();

                    if (WindowsService.Running)
                    {
                        serviceMainTaskStatus = TaskStatus.RanToCompletion;
                        Stop();
                    }
                }
                catch (System.Exception e) 
                {
                    serviceMainTaskStatus = TaskStatus.Faulted; 
                    Logging.WriteInfo(null, "*** Shutdown by {0} ***", e.ToString()); 
                    Logging.WriteException(null, e);
                    Stop();
                }
                
                Logging.WriteInfo(null, "*** Shutdown ***");

            }, serviceMainTaskCancellationTokenSource.Token);
        }

        /// <summary>
        /// This method is fired when Phabrico, running as service, is stopped
        /// </summary>
        protected override void OnStop()
        {
            serviceMainTaskCancellationTokenSource.Cancel(false);

            while (serviceMainTaskStatus == TaskStatus.Running && 
                   serviceMainTask.Status == TaskStatus.Running) 
            {
                Thread.Sleep(100);
            }

            base.OnStop();
        }
    }
}