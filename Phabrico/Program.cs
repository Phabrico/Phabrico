using Phabrico.Miscellaneous;
using System;
using System.Net;
using System.Reflection;
using System.Threading;

namespace Phabrico
{
    class Program
    {
        private Http.Server httpServer;
        private static readonly AppConfigLoader appConfigLoader = new AppConfigLoader();

        /// <summary>
        /// Main method
        /// </summary>
        public void ServiceMain()
        {
            if (Initialization())
            {
                // keep running until we get tired
                while (WindowsService.Running)
                {
                    httpServer.CleanUpSessions();
                    Thread.Sleep(250);
                }
            }

            Termination();
        }

        /// <summary>
        /// Initializes the application
        /// </summary>
        /// <returns></returns>
        private bool Initialization()
        {
            // browse to the directory where the Phabrico.exe is located
            string executable = Assembly.GetExecutingAssembly().Location;
            string directory = System.IO.Path.GetDirectoryName(executable);
            System.IO.Directory.SetCurrentDirectory(directory);

            // initialize HTTP server
            bool remoteAccessEnabled = (bool)AppConfigLoader.AppSettings["RemoteAccess"]?.Equals("Yes");
            int tcpListenPort = Int32.Parse(AppConfigLoader.AppSettings["TcpListenPort"]);

            try
            {
                httpServer = new Http.Server(remoteAccessEnabled, tcpListenPort, "/");
            }
            catch (System.Exception exception)
            {
                HttpListenerException httpListenerException = exception as HttpListenerException;
                if (httpListenerException != null && httpListenerException.ErrorCode == 32)
                {
                    string message = string.Format("TCP port {0} is already in use by another process", tcpListenPort);
                    Logging.WriteError(null, "ERROR: " + message);
                    Logging.WriteExceptionToEventLog(null, "Phabrico stopped unexpectedly: " + message, exception);
                    return false;
                }

                Logging.WriteException(null, exception);
                Logging.WriteExceptionToEventLog(null, "Phabrico stopped unexpectedly", exception);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Terminates the application
        /// </summary>
        private void Termination()
        {
            if (httpServer != null)
            {
                // terminate HTTP server
                httpServer.Stop();
            }
        }
    }
}
