using System;
using System.Collections;
using System.Threading;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public abstract partial class XmlForm
    {
        private const int OwnedThreadJoinTimeoutMilliseconds = 2000;

        private sealed class OwnedThread
        {
            public XmlForm Owner;
            public XmlFormThreadStart Start;
            public XmlFormThreadContext Context;
            public Thread Thread;
            public bool RetirementQueued;
            public bool Retired;

            public void Run()
            {
                try
                {
                    Start(Context);
                }
                finally
                {
                    Owner.CompleteOwnedThread(this);
                }
            }

            public void RetireAfterExit(object state)
            {
                Thread.Join();
                Owner.RetireOwnedThread(this);
            }
        }

        private sealed class OwnedThreadStopTimeoutException
            : InvalidOperationException
        {
            public OwnedThreadStopTimeoutException(string message)
                : base(message)
            {
            }
        }

        private readonly object _ownedThreadsSync = new object();
        private readonly ArrayList _ownedThreads = new ArrayList();
        private bool _ownedThreadsStopping;
        private bool _ownedThreadDisposalClaimed;
        private bool _closeWhenThreadsStop;
        private bool _formClosing;
        private bool _deferredClosePosted;
        private bool _deferredCloseExecuting;
        private int _deferredCloseEpoch;
        private bool _nonUserCloseRecoveryPending;
        private bool _nonUserCloseRecoveryPosted;
        private int _nonUserCloseRecoveryEpoch;
        private bool _pendingDisposalRetry;
        private bool _pendingDisposalRetryPosted;
        private bool _pendingDisposalRetryAutoAttempted;
        private int _pendingDisposalRetryEpoch;
        private int _pendingDisposalOwnerThreadId;
        private bool _pendingDisposalIdleHooked;
        private Control _pendingDisposalDispatcher;
        private Control _pendingDisposalPostedDispatcher;
        private bool _disposeInProgress;
        private bool _disposeCompleted;
        private bool _derivedDisposeCompleted;
        private XamlRuntime _disposeRuntime;
        private Control _disposeRoot;
        private Form _lifetimeForm;

        /// <summary>
        /// Starts background work immediately and makes it part of this
        /// XmlForm's lifetime. The delegate must observe StopRequested and
        /// return when the Form closes or the XmlForm is disposed. Worker code
        /// must not use synchronous Control.Invoke; update bindings directly or
        /// use <see cref="PostToUi"/> for asynchronous imperative control work.
        /// </summary>
        /// <param name="start">The non-null background delegate.</param>
        /// <returns>The started background thread.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="start"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The XML Form is closing, stopping, or disposing and cannot own new
        /// background work.
        /// </exception>
        protected Thread RunThread(XmlFormThreadStart start)
        {
            if (start == null)
                throw new ArgumentNullException("start");

            OwnedThread owned = new OwnedThread();
            owned.Owner = this;
            owned.Start = start;
            owned.Context = new XmlFormThreadContext();
            owned.Thread = new Thread(
                new ThreadStart(owned.Run));
            owned.Thread.IsBackground = true;

            lock (_ownedThreadsSync)
            {
                if (_disposed ||
                    _formClosing ||
                    _ownedThreadsStopping ||
                    _ownedThreadDisposalClaimed)
                {
                    owned.Context.Release();
                    throw new ObjectDisposedException(
                        GetType().FullName);
                }

                _ownedThreads.Add(owned);

                try
                {
                    owned.Thread.Start();
                }
                catch
                {
                    _ownedThreads.Remove(owned);
                    owned.Context.Release();
                    throw;
                }
            }

            return owned.Thread;
        }

        /// <summary>
        /// Asynchronously posts imperative work to this XML Form's UI thread.
        /// The callback is accepted only while the native Form has a live
        /// handle and the XML Form is not closing, stopping, or disposing.
        /// Prefer direct <c>PropertyBinding&lt;T&gt;.Value</c> updates for bound
        /// state.
        /// </summary>
        /// <param name="callback">The non-null UI callback.</param>
        /// <returns>
        /// <see langword="true"/> when the callback was queued on the current
        /// Form handle; otherwise <see langword="false"/> when the Form cannot
        /// accept new UI work. A later handle destruction or lifetime end can
        /// discard queued work before it runs.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="callback"/> is <see langword="null"/>.
        /// </exception>
        protected bool PostToUi(MethodInvoker callback)
        {
            if (callback == null)
                throw new ArgumentNullException("callback");

            Form form;

            lock (_ownedThreadsSync)
            {
                if (_disposed ||
                    _disposeInProgress ||
                    _formClosing ||
                    _ownedThreadsStopping ||
                    _ownedThreadDisposalClaimed)
                {
                    return false;
                }

                form = _lifetimeForm;
            }

            if (form == null ||
                form.IsDisposed ||
                form.Disposing ||
                !form.IsHandleCreated)
            {
                return false;
            }

            MethodInvoker guardedCallback =
                delegate
                {
                    lock (_ownedThreadsSync)
                    {
                        if (_disposed ||
                            _disposeInProgress ||
                            _ownedThreadsStopping ||
                            _ownedThreadDisposalClaimed ||
                            !Object.ReferenceEquals(
                                _lifetimeForm,
                                form))
                        {
                            return;
                        }
                    }

                    callback();
                };

            try
            {
                form.BeginInvoke(guardedCallback);
                return true;
            }
            catch (InvalidOperationException)
            {
                // The handle can disappear between the readiness check and
                // BeginInvoke. Treat that lifetime race like every other
                // unavailable Form instead of making workers catch it.
                if (form.IsDisposed ||
                    form.Disposing ||
                    !form.IsHandleCreated)
                {
                    return false;
                }

                throw;
            }
        }

        private void CompleteOwnedThread(OwnedThread owned)
        {
            bool queueRetirement = false;

            lock (_ownedThreadsSync)
            {
                owned.Context.Release();

                if (!owned.RetirementQueued &&
                    !owned.Retired)
                {
                    owned.RetirementQueued = true;
                    queueRetirement = true;
                }
            }

            if (queueRetirement)
            {
                bool queued = false;

                try
                {
                    queued = ThreadPool.QueueUserWorkItem(
                        new WaitCallback(owned.RetireAfterExit));
                }
                catch
                {
                }

                if (!queued)
                {
                    try
                    {
                        Thread retirementThread = new Thread(
                            new ParameterizedThreadStart(
                                owned.RetireAfterExit));
                        retirementThread.IsBackground = true;
                        retirementThread.Start(null);
                    }
                    catch
                    {
                        // Keep the entry tracked. A later bounded disposal can
                        // join and retire it synchronously.
                    }
                }
            }
        }

        private void RetireOwnedThread(OwnedThread owned)
        {
            bool retired = false;

            lock (_ownedThreadsSync)
            {
                if (!owned.Retired)
                {
                    owned.Retired = true;
                    _ownedThreads.Remove(owned);
                    retired = true;
                }
            }

            if (!retired)
                return;

            TryPostDeferredClose(owned.Thread);
            TryPostPendingDisposalRetry();
        }

        private void TryPostDeferredClose(Thread completedThread)
        {
            Form formToClose;
            int closeEpoch;

            lock (_ownedThreadsSync)
            {
                if (!_closeWhenThreadsStop ||
                    _ownedThreadDisposalClaimed ||
                    _disposed ||
                    _deferredClosePosted ||
                    _ownedThreads.Count != 0)
                {
                    return;
                }

                formToClose = _lifetimeForm;

                if (formToClose == null ||
                    formToClose.IsDisposed ||
                    !formToClose.IsHandleCreated)
                {
                    return;
                }

                _closeWhenThreadsStop = false;
                _deferredClosePosted = true;
                unchecked
                {
                    _deferredCloseEpoch++;
                }
                closeEpoch = _deferredCloseEpoch;
            }

            try
            {
                formToClose.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            lock (_ownedThreadsSync)
                            {
                                if (!_deferredClosePosted ||
                                    closeEpoch != _deferredCloseEpoch)
                                {
                                    return;
                                }

                                _deferredCloseExecuting = true;
                            }

                            try
                            {
                                // The user delegate has returned. This short
                                // join only waits for its finally block before
                                // replaying the reproducible UserClosing action.
                                if (completedThread != null)
                                    completedThread.Join();

                                if (!formToClose.IsDisposed)
                                    formToClose.Close();
                            }
                            finally
                            {
                                lock (_ownedThreadsSync)
                                {
                                    if (closeEpoch ==
                                        _deferredCloseEpoch)
                                    {
                                        _deferredCloseExecuting = false;
                                        _deferredClosePosted = false;

                                        // Another FormClosing handler may cancel
                                        // the replay. In that case the live Form
                                        // may own new work again. A successful
                                        // close detaches the lifetime first.
                                        if (!_disposed &&
                                            !_ownedThreadDisposalClaimed &&
                                            Object.ReferenceEquals(
                                                _lifetimeForm,
                                                formToClose))
                                        {
                                            _ownedThreadsStopping = false;
                                        }
                                    }
                                }
                            }
                        }));
            }
            catch (InvalidOperationException)
            {
                lock (_ownedThreadsSync)
                {
                    if (closeEpoch != _deferredCloseEpoch)
                        return;

                    _deferredCloseExecuting = false;
                    _deferredClosePosted = false;

                    if (!_disposed &&
                        Object.ReferenceEquals(
                            _lifetimeForm,
                            formToClose))
                    {
                        // Preserve the close debt. HandleCreated or another
                        // UserClosing attempt can safely retry the post.
                        _closeWhenThreadsStop = true;
                    }
                }
            }
        }

        private void TryPostNonUserCloseRecovery(Form form)
        {
            int recoveryEpoch;

            if (form == null)
                return;

            lock (_ownedThreadsSync)
            {
                if (!_nonUserCloseRecoveryPending ||
                    _nonUserCloseRecoveryPosted ||
                    _ownedThreadDisposalClaimed ||
                    _disposed ||
                    !Object.ReferenceEquals(
                        _lifetimeForm,
                        form) ||
                    form.IsDisposed ||
                    !form.IsHandleCreated)
                {
                    return;
                }

                _nonUserCloseRecoveryPosted = true;

                unchecked
                {
                    _nonUserCloseRecoveryEpoch++;
                }

                recoveryEpoch = _nonUserCloseRecoveryEpoch;
            }

            try
            {
                form.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            lock (_ownedThreadsSync)
                            {
                                if (!_nonUserCloseRecoveryPosted ||
                                    recoveryEpoch !=
                                        _nonUserCloseRecoveryEpoch)
                                {
                                    return;
                                }

                                _nonUserCloseRecoveryPosted = false;
                                _nonUserCloseRecoveryPending = false;

                                if (!_ownedThreadDisposalClaimed &&
                                    !_disposed &&
                                    !_closeWhenThreadsStop &&
                                    !form.IsDisposed &&
                                    Object.ReferenceEquals(
                                        _lifetimeForm,
                                        form))
                                {
                                    _formClosing = false;
                                    _ownedThreadsStopping = false;
                                }
                            }
                        }));
            }
            catch (InvalidOperationException)
            {
                lock (_ownedThreadsSync)
                {
                    if (recoveryEpoch !=
                        _nonUserCloseRecoveryEpoch)
                    {
                        return;
                    }

                    _nonUserCloseRecoveryPosted = false;
                    // Preserve pending recovery for HandleCreated or another
                    // non-user closing attempt.
                }
            }
        }

        private void RetainPendingDisposalRetry(
            XamlRuntime runtime)
        {
            Control root = runtime == null
                ? null
                : runtime.RootControl;
            bool hookIdle = false;
            bool createDispatcher = false;

            lock (_ownedThreadsSync)
            {
                if (_disposeCompleted)
                    return;

                if (runtime != null)
                {
                    if (_disposeRuntime != null &&
                        !Object.ReferenceEquals(
                            _disposeRuntime,
                            runtime))
                    {
                        throw new InvalidOperationException(
                            "The pending XmlForm disposal belongs to a different runtime.");
                    }

                    _disposeRuntime = runtime;

                    if (_disposeRoot == null)
                        _disposeRoot = root;
                }

                if (!_pendingDisposalRetry)
                {
                    _pendingDisposalRetry = true;
                    _pendingDisposalRetryAutoAttempted = false;
                    _pendingDisposalOwnerThreadId =
                        Thread.CurrentThread.ManagedThreadId;
                }

                if (!_pendingDisposalIdleHooked)
                {
                    _pendingDisposalIdleHooked = true;
                    hookIdle = true;
                }

                createDispatcher =
                    _pendingDisposalDispatcher == null;
            }

            if (hookIdle)
                Application.Idle += OnPendingDisposalIdle;

            if (createDispatcher)
                CreatePendingDisposalDispatcher();

            TryPostPendingDisposalRetry();
        }

        private void CreatePendingDisposalDispatcher()
        {
            Control created = null;

            try
            {
                created = new Control();

                if (created.Handle == IntPtr.Zero)
                    return;

                lock (_ownedThreadsSync)
                {
                    if (_pendingDisposalRetry &&
                        _pendingDisposalDispatcher == null)
                    {
                        _pendingDisposalDispatcher = created;
                        created = null;
                    }
                }
            }
            catch
            {
                // Application.Idle and the lifetime Form remain safe retry
                // paths when a private dispatcher cannot be created.
            }
            finally
            {
                if (created != null)
                {
                    try
                    {
                        created.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void OnPendingDisposalIdle(
            object sender,
            EventArgs e)
        {
            if (Thread.CurrentThread.ManagedThreadId !=
                _pendingDisposalOwnerThreadId)
            {
                return;
            }

            TryPostPendingDisposalRetry();
        }

        private void TryPostPendingDisposalRetry()
        {
            Control dispatcher;
            int retryEpoch;

            lock (_ownedThreadsSync)
            {
                if (!_pendingDisposalRetry ||
                    _pendingDisposalRetryPosted ||
                    _pendingDisposalRetryAutoAttempted ||
                    _disposeCompleted ||
                    _disposeInProgress ||
                    _ownedThreads.Count != 0)
                {
                    return;
                }

                dispatcher = _pendingDisposalDispatcher;

                if (dispatcher == null ||
                    dispatcher.IsDisposed ||
                    !dispatcher.IsHandleCreated)
                {
                    Form form = _lifetimeForm;

                    dispatcher = form != null &&
                        !form.IsDisposed &&
                        form.IsHandleCreated
                            ? form
                            : null;
                }

                if (dispatcher == null ||
                    dispatcher.IsDisposed ||
                    !dispatcher.IsHandleCreated)
                {
                    return;
                }

                _pendingDisposalRetryPosted = true;
                _pendingDisposalRetryAutoAttempted = true;
                _pendingDisposalPostedDispatcher = dispatcher;

                unchecked
                {
                    _pendingDisposalRetryEpoch++;
                }

                retryEpoch = _pendingDisposalRetryEpoch;
            }

            try
            {
                dispatcher.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            bool shouldRetry;

                            lock (_ownedThreadsSync)
                            {
                                if (!_pendingDisposalRetryPosted ||
                                    retryEpoch !=
                                        _pendingDisposalRetryEpoch)
                                {
                                    return;
                                }

                                _pendingDisposalRetryPosted = false;
                                _pendingDisposalPostedDispatcher = null;
                                shouldRetry =
                                    _pendingDisposalRetry &&
                                    !_disposeCompleted &&
                                    !_disposeInProgress &&
                                    _ownedThreads.Count == 0;
                            }

                            // The automatic retry is deliberately one-shot. Once
                            // its posted callback has run, keeping the static Idle
                            // subscription can only retain this XmlForm; explicit
                            // Dispose still owns any remaining cleanup debt.
                            UnhookPendingDisposalIdleAfterAutomaticAttempt();

                            if (shouldRetry)
                                RetryPendingDisposalOnOwnerThread();
                        }));
            }
            catch (InvalidOperationException)
            {
                lock (_ownedThreadsSync)
                {
                    if (retryEpoch !=
                        _pendingDisposalRetryEpoch)
                    {
                        return;
                    }

                    _pendingDisposalRetryPosted = false;
                    _pendingDisposalRetryAutoAttempted = false;
                    _pendingDisposalPostedDispatcher = null;
                }
            }
        }

        private void UnhookPendingDisposalIdleAfterAutomaticAttempt()
        {
            bool unhookIdle;

            lock (_ownedThreadsSync)
            {
                unhookIdle = _pendingDisposalIdleHooked;
                _pendingDisposalIdleHooked = false;
            }

            if (unhookIdle)
                Application.Idle -= OnPendingDisposalIdle;
        }

        private void RetryPendingDisposalOnOwnerThread()
        {
            XamlRuntime runtime;

            lock (_ownedThreadsSync)
            {
                runtime = _disposeRuntime == null
                    ? _ui
                    : _disposeRuntime;
            }

            try
            {
                if (runtime == null)
                    Dispose();
                else
                    runtime.Dispose();
            }
            catch
            {
                // Preserve the target/runtime pair and cleanup debt. Automatic
                // retry is deliberately one-shot; an explicit Dispose can retry
                // application cleanup that continues to fail.
            }
        }

        private void ClearPendingDisposalRetry()
        {
            Control dispatcher;
            bool unhookIdle;

            lock (_ownedThreadsSync)
            {
                _pendingDisposalRetry = false;
                _pendingDisposalRetryPosted = false;
                _pendingDisposalRetryAutoAttempted = false;
                _pendingDisposalPostedDispatcher = null;
                _pendingDisposalOwnerThreadId = 0;
                unhookIdle = _pendingDisposalIdleHooked;
                _pendingDisposalIdleHooked = false;
                dispatcher = _pendingDisposalDispatcher;
                _pendingDisposalDispatcher = null;

                unchecked
                {
                    _pendingDisposalRetryEpoch++;
                }
            }

            if (unhookIdle)
                Application.Idle -= OnPendingDisposalIdle;

            if (dispatcher != null)
            {
                try
                {
                    dispatcher.Dispose();
                }
                catch
                {
                }
            }
        }

        private void VerifyCanStopOwnedThreads()
        {
            Thread current = Thread.CurrentThread;

            lock (_ownedThreadsSync)
            {
                int i;

                for (i = 0; i < _ownedThreads.Count; i++)
                {
                    OwnedThread owned =
                        (OwnedThread)_ownedThreads[i];

                    if (Object.ReferenceEquals(
                        owned.Thread,
                        current))
                    {
                        throw new InvalidOperationException(
                            "An XmlForm cannot be disposed by one of its own " +
                            "RunThread delegates. Marshal disposal to the Form thread.");
                    }
                }
            }
        }

        private void StopOwnedThreads()
        {
            ArrayList snapshot;

            lock (_ownedThreadsSync)
            {
                _ownedThreadDisposalClaimed = true;
                _ownedThreadsStopping = true;
                _closeWhenThreadsStop = false;
                _deferredClosePosted = false;
                _deferredCloseExecuting = false;
                _nonUserCloseRecoveryPending = false;
                _nonUserCloseRecoveryPosted = false;

                unchecked
                {
                    _deferredCloseEpoch++;
                    _nonUserCloseRecoveryEpoch++;
                }

                snapshot = new ArrayList(_ownedThreads);
                int i;

                for (i = 0; i < snapshot.Count; i++)
                {
                    ((OwnedThread)snapshot[i]).Context.RequestStop();
                }
            }

            int threadIndex;
            int startedAt = Environment.TickCount;

            for (threadIndex = 0;
                threadIndex < snapshot.Count;
                threadIndex++)
            {
                OwnedThread owned =
                    (OwnedThread)snapshot[threadIndex];
                int elapsed = unchecked(
                    Environment.TickCount - startedAt);
                int remaining =
                    OwnedThreadJoinTimeoutMilliseconds - elapsed;

                if (remaining < 0)
                    remaining = 0;

                if (!owned.Thread.Join(remaining))
                {
                    throw new OwnedThreadStopTimeoutException(
                        "A RunThread delegate did not stop within " +
                        OwnedThreadJoinTimeoutMilliseconds.ToString() +
                        " milliseconds. It must observe StopRequested, avoid " +
                        "synchronous Control.Invoke, and return before disposal " +
                        "can be retried safely.");
                }

                RetireOwnedThread(owned);
            }
        }

        private void AttachFormLifetime(Form form)
        {
            lock (_ownedThreadsSync)
            {
                if (Object.ReferenceEquals(_lifetimeForm, form))
                    return;
            }

            DetachFormLifetime();

            lock (_ownedThreadsSync)
                _lifetimeForm = form;

            form.HandleCreated +=
                new EventHandler(OnOwnedFormHandleCreated);
            form.HandleDestroyed +=
                new EventHandler(OnOwnedFormHandleDestroyed);
            form.FormClosing +=
                new FormClosingEventHandler(OnOwnedFormClosing);
            form.FormClosed +=
                new FormClosedEventHandler(OnOwnedFormClosed);
            form.Disposed +=
                new EventHandler(OnOwnedFormDisposed);
        }

        private void DetachFormLifetime()
        {
            Form form;

            lock (_ownedThreadsSync)
            {
                form = _lifetimeForm;
                _lifetimeForm = null;
                _formClosing = false;
                _closeWhenThreadsStop = false;
                _deferredClosePosted = false;
                _deferredCloseExecuting = false;
                _nonUserCloseRecoveryPending = false;
                _nonUserCloseRecoveryPosted = false;

                unchecked
                {
                    _deferredCloseEpoch++;
                    _nonUserCloseRecoveryEpoch++;
                }
            }

            if (form == null)
                return;

            form.HandleCreated -=
                new EventHandler(OnOwnedFormHandleCreated);
            form.HandleDestroyed -=
                new EventHandler(OnOwnedFormHandleDestroyed);
            form.FormClosing -=
                new FormClosingEventHandler(OnOwnedFormClosing);
            form.FormClosed -=
                new FormClosedEventHandler(OnOwnedFormClosed);
            form.Disposed -=
                new EventHandler(OnOwnedFormDisposed);
        }

        private void OnOwnedFormClosed(
            object sender,
            FormClosedEventArgs e)
        {
            Dispose();
        }

        private void OnOwnedFormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            Form form = sender as Form;
            bool postNonUserRecovery = false;

            lock (_ownedThreadsSync)
            {
                if (_ownedThreadDisposalClaimed)
                {
                    if (!_disposed &&
                        !e.Cancel &&
                        _ownedThreads.Count != 0 &&
                        e.CloseReason == CloseReason.UserClosing)
                    {
                        // The explicit disposal retry owns the eventual root
                        // teardown. Do not let a later user close destroy the
                        // Form while a timed-out worker is still active.
                        e.Cancel = true;
                    }

                    return;
                }

                if (_disposed ||
                    _disposeInProgress)
                {
                    return;
                }

                _formClosing = true;

                if (e.Cancel ||
                    _ownedThreads.Count == 0)
                {
                    if (!_deferredClosePosted)
                    {
                        _closeWhenThreadsStop = false;
                        _ownedThreadsStopping = false;
                    }

                    if (!_nonUserCloseRecoveryPending)
                        _nonUserCloseRecoveryPending = true;

                    postNonUserRecovery =
                        !_nonUserCloseRecoveryPosted;
                }
                else
                {
                    _ownedThreadsStopping = true;
                    int i;

                    for (i = 0; i < _ownedThreads.Count; i++)
                    {
                        ((OwnedThread)_ownedThreads[i])
                            .Context.RequestStop();
                    }

                    if (e.CloseReason == CloseReason.UserClosing)
                    {
                        _nonUserCloseRecoveryPending = false;
                        _nonUserCloseRecoveryPosted = false;

                        unchecked
                        {
                            _nonUserCloseRecoveryEpoch++;
                        }

                        _closeWhenThreadsStop = true;

                        // UserClosing can be reproduced with Form.Close. Return to
                        // the message loop and replay it after every worker exits,
                        // avoiding the UI Join versus synchronous Invoke deadlock.
                        e.Cancel = true;
                    }
                    else
                    {
                        // A later FormClosing subscriber can still cancel this
                        // non-reproducible close. Recover worker admission only
                        // after the complete event invocation leaves the Form live.
                        if (!_nonUserCloseRecoveryPending)
                            _nonUserCloseRecoveryPending = true;

                        postNonUserRecovery =
                            !_nonUserCloseRecoveryPosted;
                    }
                }
            }

            if (postNonUserRecovery && form != null)
                TryPostNonUserCloseRecovery(form);
        }

        private void OnOwnedFormHandleCreated(
            object sender,
            EventArgs e)
        {
            TryPostDeferredClose(null);
            TryPostNonUserCloseRecovery(sender as Form);
            TryPostPendingDisposalRetry();
        }

        private void OnOwnedFormHandleDestroyed(
            object sender,
            EventArgs e)
        {
            lock (_ownedThreadsSync)
            {
                if (!_disposed &&
                    _deferredClosePosted &&
                    !_deferredCloseExecuting)
                {
                    // A BeginInvoke is tied to the native handle that accepted
                    // it. Re-arm the close for the next HandleCreated.
                    _deferredClosePosted = false;
                    _closeWhenThreadsStop = true;

                    unchecked
                    {
                        _deferredCloseEpoch++;
                    }
                }

                if (!_disposed &&
                    _nonUserCloseRecoveryPosted)
                {
                    _nonUserCloseRecoveryPosted = false;
                    _nonUserCloseRecoveryPending = true;

                    unchecked
                    {
                        _nonUserCloseRecoveryEpoch++;
                    }
                }

                if (_pendingDisposalRetryPosted &&
                    Object.ReferenceEquals(
                        _pendingDisposalPostedDispatcher,
                        sender))
                {
                    _pendingDisposalRetryPosted = false;
                    _pendingDisposalRetryAutoAttempted = false;
                    _pendingDisposalPostedDispatcher = null;

                    unchecked
                    {
                        _pendingDisposalRetryEpoch++;
                    }
                }
            }
        }

        private void OnOwnedFormDisposed(
            object sender,
            EventArgs e)
        {
            Dispose();
        }

        private bool DisposeXmlForm(
            bool disposeRuntime,
            XamlRuntime owningRuntime)
        {
            SealIncludeRequests();

            if (_disposeCompleted)
                return true;

            if (_disposeInProgress)
                return false;

            XamlRuntime loaded = _disposeRuntime;

            if (loaded == null)
            {
                loaded = _ui == null
                    ? owningRuntime
                    : _ui;
            }

            if (owningRuntime != null &&
                loaded != null &&
                !Object.ReferenceEquals(
                    loaded,
                    owningRuntime))
            {
                throw new InvalidOperationException(
                    "The XmlForm is paired with a different runtime.");
            }

            if (loaded != null)
                loaded.VerifyCanDispose();

            VerifyCanStopOwnedThreads();
            _disposeInProgress = true;

            try
            {
                Exception firstError = null;
                bool workersStopped = false;

                if (loaded != null)
                {
                    _disposeRuntime = loaded;

                    if (_disposeRoot == null)
                        _disposeRoot = loaded.RootControl;
                }

                try
                {
                    StopOwnedThreads();
                    workersStopped = true;
                }
                catch (Exception ex)
                {
                    firstError = ex;

                    if (ex is OwnedThreadStopTimeoutException)
                    {
                        try
                        {
                            RetainPendingDisposalRetry(loaded);
                        }
                        catch
                        {
                            // Preserve the bounded-stop failure. The caller still
                            // owns an explicit retry path.
                        }
                    }
                }

                // Do not detach the Form or make the wrapper unusable until
                // every worker has actually returned. A timed-out attempt
                // therefore leaves wrapper/runtime/control teardown retryable.
                if (workersStopped && !_disposed)
                {
                    _disposed = true;
                    _loadedNotificationRaised = false;
                    DetachFormLifetime();
                }

                Control root = _disposeRoot;

                if (workersStopped &&
                    root != null &&
                    !root.IsDisposed &&
                    !root.Disposing)
                {
                    try
                    {
                        root.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                // Dispose the native tree while its runtime is still usable.
                // ItemsControl and component cleanup can legitimately call
                // back into the runtime while child controls are released.
                // The root Disposed hook normally releases the runtime for us;
                // the explicit call afterward is the idempotent completion or
                // retry pass and also covers roots whose hook was never
                // installed.
                if (workersStopped &&
                    disposeRuntime &&
                    loaded != null)
                {
                    try
                    {
                        loaded.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                if (workersStopped && !_derivedDisposeCompleted)
                {
                    try
                    {
                        Dispose(true);
                        _derivedDisposeCompleted = true;
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                if (firstError != null)
                {
                    throw new InvalidOperationException(
                        "One or more XML Form resources could not be released: " +
                        firstError.Message,
                        firstError);
                }

                _ui = null;
                _disposeRuntime = null;
                _disposeRoot = null;
                _runtimeLoadFailed = false;
                _disposeCompleted = true;
                ClearPendingDisposalRetry();

                if (loaded != null)
                {
                    loaded.ReleaseCompletedXmlFormLifetime(
                        this);
                }

                GC.SuppressFinalize(this);
                return true;
            }
            finally
            {
                _disposeInProgress = false;
            }
        }

        private void PrepareXmlFormForOwningRuntimeDisposal(
            XamlRuntime runtime)
        {
            if (_disposeCompleted)
                return;

            XamlRuntime pairedRuntime = _disposeRuntime == null
                ? _ui
                : _disposeRuntime;

            if (pairedRuntime != null &&
                !Object.ReferenceEquals(
                    pairedRuntime,
                    runtime))
            {
                throw new InvalidOperationException(
                    "The XmlForm is paired with a different runtime.");
            }

            if (_disposeInProgress)
                return;

            VerifyCanStopOwnedThreads();
            _disposeRuntime = runtime;

            if (_disposeRoot == null)
                _disposeRoot = runtime.RootControl;

            try
            {
                StopOwnedThreads();
            }
            catch (OwnedThreadStopTimeoutException)
            {
                try
                {
                    RetainPendingDisposalRetry(runtime);
                }
                catch
                {
                    // Preserve the bounded-stop failure.
                }

                throw;
            }

            if (_disposed)
                return;

            _disposed = true;
            _loadedNotificationRaised = false;
            DetachFormLifetime();
        }

        internal bool HasTrackedOwnedThreads
        {
            get
            {
                lock (_ownedThreadsSync)
                    return _ownedThreads.Count != 0;
            }
        }

        internal void RetainFailedLoadDisposalRetry(
            XamlRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException("runtime");

            XamlRuntime pairedRuntime = _disposeRuntime == null
                ? _ui
                : _disposeRuntime;

            if (pairedRuntime != null &&
                !Object.ReferenceEquals(
                    pairedRuntime,
                    runtime))
            {
                throw new InvalidOperationException(
                    "The failed XmlForm load belongs to a different runtime.");
            }

            RetainPendingDisposalRetry(runtime);
        }
    }
}
