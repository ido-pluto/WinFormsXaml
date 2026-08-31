using System;
using System.Threading;

namespace WinFormsXaml
{
    /// <summary>
    /// Supplies cooperative shutdown state to work started by
    /// <see cref="XmlForm.RunThread(XmlFormThreadStart)"/>.
    /// </summary>
    public sealed class XmlFormThreadContext
    {
        private ManualResetEvent _stopEvent;

        internal XmlFormThreadContext()
        {
            _stopEvent = new ManualResetEvent(false);
        }

        /// <summary>
        /// Gets whether the Form has closed or its XmlForm is being disposed.
        /// Long-running work must return promptly when this becomes true.
        /// </summary>
        public bool StopRequested
        {
            get
            {
                ManualResetEvent stopEvent = _stopEvent;

                return stopEvent == null ||
                    stopEvent.WaitOne(0, false);
            }
        }

        /// <summary>
        /// Gets a wait handle that is signaled when the work must stop. This
        /// lets blocking loops wait without polling.
        /// </summary>
        public WaitHandle StopWaitHandle
        {
            get
            {
                ManualResetEvent stopEvent = _stopEvent;

                if (stopEvent == null)
                {
                    throw new ObjectDisposedException(
                        typeof(XmlFormThreadContext).FullName);
                }

                return stopEvent;
            }
        }

        internal void RequestStop()
        {
            ManualResetEvent stopEvent = _stopEvent;

            if (stopEvent != null)
                stopEvent.Set();
        }

        internal void Release()
        {
            ManualResetEvent stopEvent = _stopEvent;
            _stopEvent = null;

            if (stopEvent != null)
                stopEvent.Close();
        }
    }

    /// <summary>
    /// Represents cooperative background work owned by an <see cref="XmlForm"/>.
    /// </summary>
    /// <param name="context">
    /// Shutdown state owned by the XmlForm. The delegate must not retain it
    /// after returning.
    /// </param>
    public delegate void XmlFormThreadStart(
        XmlFormThreadContext context);
}
