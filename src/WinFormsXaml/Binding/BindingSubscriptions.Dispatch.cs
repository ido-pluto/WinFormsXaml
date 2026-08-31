using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private void QueueObservableBindingDispatch()
        {
            QueueObservableBindingDispatch(true, true);
        }

        private void QueueObservableBindingDispatch(
            bool mayRetryPost,
            bool allowSynchronousDrain)
        {
            Control root;
            Control dispatcher;
            bool drainSynchronously;
            bool drainCallbacksSynchronously;
            int postEpoch = 0;

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    _observableDisposalClaimed ||
                    !_observableDispatchDebt ||
                    _observableDispatchRunning ||
                    _observableSynchronousDispatchActive)
                {
                    return;
                }

                root = RootControl;
                dispatcher = root == null
                    ? _observableRootlessDispatcher
                    : root;

                if (dispatcher == null)
                    return;

                drainSynchronously =
                    allowSynchronousDrain &&
                    root == null &&
                    System.Threading.Thread.CurrentThread.ManagedThreadId ==
                        _observableOwnerThreadId;
                drainCallbacksSynchronously =
                    allowSynchronousDrain &&
                    root != null &&
                    System.Threading.Thread.CurrentThread.ManagedThreadId ==
                        _observableOwnerThreadId &&
                    HasPendingSynchronousCallbackUnderLock();

                if (_observableDispatchQueued &&
                    !drainSynchronously &&
                    !drainCallbacksSynchronously)
                {
                    return;
                }

                _observableDispatchQueued = true;

                if (drainSynchronously ||
                    drainCallbacksSynchronously)
                {
                    _observableSynchronousDispatchActive = true;
                }
                else
                {
                    postEpoch = ++_observableDispatchPostEpoch;
                }
            }

            if (drainSynchronously)
            {
                DrainObservableBindingChangesSynchronously();
                return;
            }

            if (drainCallbacksSynchronously)
            {
                DrainObservableCallbacksSynchronously();
                return;
            }

            if (dispatcher.IsDisposed)
            {
                RetainObservableDispatchDebt(postEpoch);
                return;
            }

            if (!dispatcher.IsHandleCreated)
            {
                RetainObservableDispatchDebt(postEpoch);

                // Close the HandleCreated race: its event can run after the
                // check above but before queued ownership is released.
                if (mayRetryPost &&
                    !dispatcher.IsDisposed &&
                    dispatcher.IsHandleCreated)
                {
                    QueueObservableBindingDispatch(
                        false,
                        allowSynchronousDrain);
                }

                return;
            }

            try
            {
                dispatcher.BeginInvoke(
                    (MethodInvoker)delegate
                    {
                        DrainObservableBindingChanges(postEpoch);
                    });
            }
            catch (ObjectDisposedException)
            {
                RetainObservableDispatchDebt(postEpoch);

                if (mayRetryPost &&
                    !dispatcher.IsDisposed &&
                    dispatcher.IsHandleCreated)
                {
                    QueueObservableBindingDispatch(
                        false,
                        allowSynchronousDrain);
                }
            }
            catch (InvalidOperationException)
            {
                RetainObservableDispatchDebt(postEpoch);

                if (mayRetryPost &&
                    !dispatcher.IsDisposed &&
                    dispatcher.IsHandleCreated)
                {
                    QueueObservableBindingDispatch(
                        false,
                        allowSynchronousDrain);
                }
            }
        }

        private void RetainObservableDispatchDebt(int postEpoch)
        {
            lock (_observableBindingSync)
            {
                if (!_observableDispatchQueued ||
                    _observableDispatchPostEpoch != postEpoch)
                {
                    return;
                }

                _observableDispatchQueued = false;

                if (!_observableBindingSubscriptionsDisposed)
                    RefreshObservableDispatchDebtUnderLock();
            }
        }

        private void DrainObservableBindingChanges(int postEpoch)
        {
            Exception failure =
                DrainObservableBindingBatch(
                    postEpoch,
                    true,
                    false);
            bool queueAgain;

            lock (_observableBindingSync)
            {
                queueAgain =
                    _observableDispatchDebt &&
                    !_observableBindingSubscriptionsDisposed;
            }

            if (queueAgain)
            {
                // This method is already running from BeginInvoke. Keep a
                // rootless continuation posted as a separate batch so a later
                // failure cannot mask this batch's earlier failure.
                QueueObservableBindingDispatch(true, false);
            }

            ThrowObservableBindingFailure(failure);
        }

        private void DrainObservableBindingChangesSynchronously()
        {
            Exception firstFailure = null;
            bool released = false;

            try
            {
                while (true)
                {
                    Exception failure =
                        DrainObservableBindingBatch(
                            0,
                            false,
                            false);

                    if (firstFailure == null && failure != null)
                        firstFailure = failure;

                    lock (_observableBindingSync)
                    {
                        if (_observableBindingSubscriptionsDisposed ||
                            !_observableDispatchDebt)
                        {
                            _observableSynchronousDispatchActive = false;
                            released = true;
                            break;
                        }

                        // Claim the next reentrant batch without recursing.
                        _observableDispatchQueued = true;
                    }
                }
            }
            finally
            {
                if (!released)
                {
                    lock (_observableBindingSync)
                    {
                        _observableSynchronousDispatchActive = false;
                        _observableDispatchQueued = false;

                        if (!_observableBindingSubscriptionsDisposed)
                            RefreshObservableDispatchDebtUnderLock();
                    }
                }
            }

            ThrowObservableBindingFailure(firstFailure);
        }

        private void DrainObservableCallbacksSynchronously()
        {
            Exception firstFailure = null;
            bool released = false;
            bool queueRemaining = false;

            try
            {
                while (true)
                {
                    Exception failure =
                        DrainObservableBindingBatch(
                            0,
                            false,
                            true);

                    if (firstFailure == null && failure != null)
                        firstFailure = failure;

                    lock (_observableBindingSync)
                    {
                        if (_observableBindingSubscriptionsDisposed ||
                            !HasPendingSynchronousCallbackUnderLock())
                        {
                            _observableSynchronousDispatchActive = false;
                            queueRemaining =
                                _observableDispatchDebt &&
                                !_observableBindingSubscriptionsDisposed;
                            released = true;
                            break;
                        }

                        // Claim the next synchronous batch without recursing.
                        _observableDispatchQueued = true;
                    }
                }
            }
            finally
            {
                if (!released)
                {
                    lock (_observableBindingSync)
                    {
                        _observableSynchronousDispatchActive = false;
                        _observableDispatchQueued = false;

                        if (!_observableBindingSubscriptionsDisposed)
                        {
                            RefreshObservableDispatchDebtUnderLock();
                            queueRemaining = _observableDispatchDebt;
                        }
                    }
                }
            }

            if (queueRemaining)
                QueueObservableBindingDispatch(true, true);

            ThrowObservableBindingFailure(firstFailure);
        }

        private bool HasPendingSynchronousCallbackUnderLock()
        {
            int i;

            for (i = 0;
                 _observablePendingRegistrations != null &&
                 i < _observablePendingRegistrations.Count;
                 i++)
            {
                ObservableBindingRegistration registration =
                    _observablePendingRegistrations[i] as
                        ObservableBindingRegistration;

                if (IsObservableBindingActiveUnderLock(registration) &&
                    (HasCurrentObservableTargetChange(registration) ||
                     (IsManagedObservableCallback(registration) &&
                      registration.PendingSource &&
                      registration.PendingSourceRevision ==
                        registration.Revision)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCurrentObservableTargetChange(
            ObservableBindingRegistration registration)
        {
            return
                registration != null &&
                registration.PendingTarget &&
                registration.HasPendingTargetSourceVersion &&
                registration.PendingTargetRevision == registration.Revision;
        }

        private static bool IsManagedObservableCallback(
            ObservableBindingRegistration registration)
        {
            return registration != null &&
                (registration.TargetRuntimeBinding != null ||
                 registration.Owner is RenderBindingSlot);
        }

        private Exception DrainObservableBindingBatch(
            int postEpoch,
            bool verifyPostEpoch,
            bool synchronousCallbacksOnly)
        {
            ArrayList work = new ArrayList();

            lock (_observableBindingSync)
            {
                if (verifyPostEpoch &&
                    (!_observableDispatchQueued ||
                     _observableDispatchPostEpoch != postEpoch))
                {
                    return null;
                }

                _observableDispatchQueued = false;

                if (_observableBindingSubscriptionsDisposed ||
                    _dynamicFeaturesDisposed)
                {
                    _observableDispatchDebt = false;
                    return null;
                }

                _observableDispatchRunning = true;
                _observableDispatchDebt = false;

                ArrayList pendingRegistrations =
                    _observablePendingRegistrations;
                _observablePendingRegistrations = new ArrayList();

                int i;

                for (i = 0;
                     pendingRegistrations != null &&
                     i < pendingRegistrations.Count;
                     i++)
                {
                    ObservableBindingRegistration registration =
                        pendingRegistrations[i] as
                            ObservableBindingRegistration;

                    // Detached registrations must fall through so their stale
                    // queued flag is released instead of surviving every
                    // target-only synchronous drain.
                    if (synchronousCallbacksOnly &&
                        registration != null &&
                        IsObservableBindingActiveUnderLock(registration) &&
                        !IsManagedObservableCallback(registration) &&
                        !HasCurrentObservableTargetChange(registration))
                    {
                        _observablePendingRegistrations.Add(
                            registration);
                        continue;
                    }

                    if (registration != null)
                        registration.PendingDispatchQueued = false;

                    if (!IsObservableBindingActiveUnderLock(registration))
                        continue;

                    bool sourceCurrent =
                        registration.PendingSource &&
                        registration.PendingSourceRevision ==
                            registration.Revision;
                    bool targetCurrent =
                        registration.PendingTarget &&
                        registration.HasPendingTargetSourceVersion &&
                        registration.PendingTargetRevision ==
                            registration.Revision;

                    ObservableDispatchWork item = null;

                    if (sourceCurrent || targetCurrent)
                    {
                        item = new ObservableDispatchWork();
                        item.Registration = registration;
                        item.Revision = registration.Revision;
                        item.SourceMayRebind =
                            sourceCurrent &&
                            registration.PendingSourceMayRebind;
                        item.ReplayTargetAfterRebind =
                            item.SourceMayRebind &&
                            targetCurrent;
                        item.TargetToSource =
                            !item.SourceMayRebind &&
                            targetCurrent &&
                            (!sourceCurrent ||
                             registration.PendingTargetOrder >
                                registration.PendingSourceOrder);
                        item.ExpectedSourceVersion =
                            registration.PendingTargetSourceVersion;
                        item.TargetOrder =
                            registration.PendingTargetOrder;
                        item.SourceSignals =
                            sourceCurrent &&
                            registration.PendingSourceSignals != null
                                ? new ArrayList(
                                    registration.PendingSourceSignals)
                                : null;
                        item.CapturedTargetValue =
                            registration.PendingTargetValue;

                        if (item.ReplayTargetAfterRebind)
                        {
                            // Replay candidates are ordered by the captured edit.
                            // A stale branch event may have a later source stamp,
                            // but must not make an older edit run ahead of a newer
                            // edit on another target sharing the replacement source.
                            item.Order = registration.PendingTargetOrder;
                        }
                        else
                        {
                            item.Order = item.TargetToSource
                                ? registration.PendingTargetOrder
                                : registration.PendingSourceOrder;
                        }
                    }

                    ClearObservablePendingUnderLock(registration);

                    if (item != null)
                        work.Add(item);
                }
            }

            Exception firstFailure = null;

            try
            {
                work.Sort(_observableDispatchWorkComparer);
                int i;

                for (i = 0; i < work.Count; i++)
                {
                    ObservableDispatchWork item =
                        work[i] as ObservableDispatchWork;

                    try
                    {
                        if (item != null && item.SourceMayRebind)
                        {
                            ApplyObservableRebindingSourceChange(item);
                        }
                        else if (item != null && item.TargetToSource)
                        {
                            ApplyObservableTargetChange(
                                item.Registration,
                                item.Revision,
                                item.ExpectedSourceVersion,
                                item.CapturedTargetValue,
                                item.TargetOrder);
                        }
                        else if (item != null)
                        {
                            ApplyObservableSourceChange(
                                item.Registration,
                                item.Revision);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (firstFailure == null)
                            firstFailure = ex;
                    }
                }
            }
            finally
            {
                lock (_observableBindingSync)
                {
                    _observableDispatchRunning = false;
                    RefreshObservableDispatchDebtUnderLock();
                }
            }

            return firstFailure;
        }

        private static void ThrowObservableBindingFailure(
            Exception failure)
        {
            if (failure != null)
            {
                throw new InvalidOperationException(
                    "An observable binding update failed.",
                    failure);
            }
        }

        private void ApplyObservableSourceChange(
            ObservableBindingRegistration registration,
            long revision)
        {
            ObservableBindingChangedCallback callback;
            object owner;
            object target;

            lock (_observableBindingSync)
            {
                if (!IsObservableBindingActiveUnderLock(registration) ||
                    registration.Revision != revision)
                {
                    return;
                }

                callback = registration.Callback;
                owner = registration.Owner;
                target = registration.Target;
            }

            if (IsDisposedTarget(target))
            {
                DetachObservableBinding(registration);
                return;
            }

            if (callback != null &&
                IsObservableBindingCurrent(
                    registration,
                    owner,
                    revision))
            {
                callback(owner, revision);
            }
        }

        private void ApplyObservableRebindingSourceChange(
            ObservableDispatchWork item)
        {
            if (item == null)
                return;

            ApplyObservableSourceChange(
                item.Registration,
                item.Revision);

            if (!item.ReplayTargetAfterRebind)
                return;

            ObservableBindingRegistration registration =
                item.Registration;
            BindingPathDependency terminal;
            long revision;

            lock (_observableBindingSync)
            {
                long capturedSourceOrder =
                    GetLatestCurrentObservableSourceSignalOrderUnderLock(
                        registration,
                        item.SourceSignals);
                long pendingSourceOrder =
                    registration.PendingSource &&
                    registration.PendingSourceRevision ==
                        registration.Revision
                        ? GetLatestCurrentObservableSourceSignalOrderUnderLock(
                            registration,
                            registration.PendingSourceSignals)
                        : 0;

                if (!IsObservableBindingActiveUnderLock(registration) ||
                    registration.Mode != BindingMode.TwoWay ||
                    capturedSourceOrder > item.TargetOrder ||
                    pendingSourceOrder > item.TargetOrder ||
                    (registration.PendingTarget &&
                     registration.PendingTargetRevision ==
                        registration.Revision &&
                     registration.PendingTargetOrder > item.TargetOrder))
                {
                    return;
                }

                terminal = registration.TerminalDependency;
                revision = registration.Revision;
            }

            if (terminal == null)
                return;

            long expectedSourceVersion;

            lock (_observableBindingSync)
            {
                if (!IsObservableBindingActiveUnderLock(registration) ||
                    registration.Revision != revision ||
                    !Object.ReferenceEquals(
                        registration.TerminalDependency,
                        terminal))
                {
                    return;
                }

                expectedSourceVersion =
                    GetObservableSourceVersionUnderLock(terminal);
            }

            bool replayed = ApplyObservableTargetChange(
                registration,
                revision,
                expectedSourceVersion,
                item.CapturedTargetValue,
                item.TargetOrder);

            if (!replayed)
                return;

            // The source-change callback above must re-resolve an intermediate
            // path before the captured edit can be replayed to its new terminal.
            // Dynamic bindings also apply that terminal's old value to the target
            // while re-resolving, so publish the successful replay once more.
            // Item bindings use the same callback to queue their normal refresh.
            ApplyObservableSourceChange(registration, revision);
        }

        private bool ApplyObservableTargetChange(
            ObservableBindingRegistration registration,
            long revision,
            long expectedSourceVersion,
            object value,
            long order)
        {
            BindingPathDependency terminal;

            lock (_observableBindingSync)
            {
                if (!IsObservableBindingActiveUnderLock(registration) ||
                    registration.Revision != revision ||
                    registration.Mode != BindingMode.TwoWay ||
                    HasNewerObservableSignalUnderLock(
                        registration,
                        order))
                {
                    return false;
                }

                terminal = registration.TerminalDependency;
                registration.SourceWriteDepth++;
                registration.SourceWriteSource = terminal.Source;
                registration.SourceWriteRuntimeBinding =
                    terminal.RuntimeBinding;
                registration.SourceWriteRevision = revision;
                registration.SourceWriteExpectedVersion =
                    unchecked(expectedSourceVersion + 1);
            }

            try
            {
                if (IsDisposedTarget(registration.Target))
                {
                    DetachObservableBinding(registration);
                    return false;
                }

                lock (_observableBindingSync)
                {
                    if (!IsObservableBindingActiveUnderLock(
                            registration) ||
                        registration.Revision != revision ||
                        !Object.ReferenceEquals(
                            registration.TerminalDependency,
                            terminal) ||
                        HasNewerObservableSignalUnderLock(
                            registration,
                            order))
                    {
                        return false;
                    }
                }

                bool versionConflict;

                if (!TrySetObservableSourceValue(
                        terminal,
                        value,
                        expectedSourceVersion,
                        out versionConflict))
                {
                    if (versionConflict)
                    {
                        lock (_observableBindingSync)
                        {
                            if (IsObservableBindingActiveUnderLock(
                                    registration) &&
                                registration.Revision == revision)
                            {
                                MarkObservableSourcePendingUnderLock(
                                    registration,
                                    false);
                            }
                        }
                    }

                    // Temporary editor values such as an empty numeric field
                    // are normal user input. Keep the target value and leave the
                    // typed source unchanged until a later edit converts.
                    return false;
                }
                else
                {
                    bool committed = false;

                    lock (_observableBindingSync)
                    {
                        if (IsObservableBindingActiveUnderLock(registration) &&
                            registration.Revision == revision &&
                            Object.ReferenceEquals(
                                registration.TerminalDependency,
                                terminal) &&
                            !HasNewerObservableSignalUnderLock(
                                registration,
                                order))
                        {
                            OnObservableTargetValueCommitted(
                                registration.Owner,
                                value);

                            if (terminal.RuntimeBinding == null)
                            {
                                // A conforming INotifyPropertyChanged setter
                                // normally queued this refresh synchronously.
                                // If it did not, reconcile this runtime anyway;
                                // external changes still require notifications.
                                MarkObservableSourcePendingUnderLock(
                                    registration,
                                    false);
                            }

                            committed = true;
                        }
                    }

                    return committed;
                }
            }
            finally
            {
                lock (_observableBindingSync)
                {
                    if (registration.SourceWriteDepth > 0)
                        registration.SourceWriteDepth--;

                    if (registration.SourceWriteDepth == 0)
                    {
                        registration.SourceWriteSource = null;
                        registration.SourceWriteRuntimeBinding = null;
                        registration.SourceWriteRevision = 0;
                        registration.SourceWriteExpectedVersion = 0;
                    }
                }
            }
        }
    }
}
