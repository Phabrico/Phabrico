using System;
using System.Threading;

namespace Phabrico.Http
{
    internal class UnimpersonatedTask
    {
        public UnimpersonatedTask(Action<CancellationToken> action)
        {
            Action = action;
            CancellationTokenSource = new CancellationTokenSource();
            ManualResetEvent = new ManualResetEvent(false);
            IsRunning = false;
        }

        public Action<CancellationToken> Action { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public ManualResetEvent ManualResetEvent { get; set; }
        public bool IsRunning { get; set; }
    }
}