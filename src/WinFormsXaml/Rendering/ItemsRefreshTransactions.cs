using System;
using System.Collections;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private void BuildItemsRefreshBatch(
            ItemsRefreshState state,
            int maximum)
        {
            int built = 0;

            while (state.BuildIndex <
                       state.BuildQueue.Count &&
                   built < maximum &&
                   IsItemsRefreshCurrent(state))
            {
                RenderedItemRecord record =
                    (RenderedItemRecord)state.BuildQueue[
                        state.BuildIndex];

                ArrayList bindingSlots;

                Control child =
                    BuildTemplateControl(
                        state.Host,
                        state.Host.TemplateRoot,
                        record.Item,
                        record.FunctionResults,
                        out bindingSlots);

                if (!IsItemsRefreshCurrent(state))
                {
                    if (child != null)
                        ReleaseCreatedElement(child);

                    return;
                }

                // From this point the refresh state owns the tree. Publish the
                // reference before inheritance/layout so rollback can dispose it
                // if either of those later phases throws.
                record.Control = child;
                record.BindingSlots = bindingSlots;
                record.MeasureCacheValid = false;

                ActivateRenderedItemRecordBindings(
                    record,
                    state.Host,
                    record.Item);

                if (!IsItemsRefreshCurrent(state))
                    return;

                state.AnyVisualChange = true;
                state.AnyLayoutChange = true;

                if (child != null)
                {
                    ApplyInheritedProperties(
                        child,
                        state.Host);

                    if (!IsItemsRefreshCurrent(state))
                        return;

                    PerformLayoutRecursive(child);

                    if (!IsItemsRefreshCurrent(state))
                        return;

                    ElementInfo rootInfo = GetInfo(child);
                    record.RootVisibility = rootInfo.VisibilityCollapsed
                        ? ItemVisibilityState.Collapsed
                        : rootInfo.Hidden
                            ? ItemVisibilityState.Hidden
                            : ItemVisibilityState.Visible;
                    record.RootConditionVisible =
                        GetInitialRootConditionVisibility(
                            record.BindingSlots,
                            child);
                    ApplyItemRootVisibility(record);
                }

                state.BuildIndex++;
                built++;
            }
        }

        private bool GetInitialRootConditionVisibility(
            ArrayList bindingSlots,
            Control root)
        {
            bool visible = true;
            ElementInfo info;

            if (root != null &&
                _elementInfos.TryGetValue(root, out info) &&
                info.ConditionStates != null)
            {
                IDictionaryEnumerator conditions =
                    info.ConditionStates.GetEnumerator();

                while (conditions.MoveNext())
                {
                    if (Object.ReferenceEquals(
                            conditions.Key,
                            _itemRootConditionStateKey))
                    {
                        continue;
                    }

                    object conditionValue = conditions.Value;

                    if (conditionValue is bool &&
                        !(bool)conditionValue)
                    {
                        visible = false;
                    }
                }
            }

            int i;

            for (i = 0;
                 bindingSlots != null && i < bindingSlots.Count;
                 i++)
            {
                RenderBindingSlot slot =
                    bindingSlots[i] as RenderBindingSlot;

                if (slot == null ||
                    slot.Kind != RenderBindingSlotKind.Condition ||
                    !Object.ReferenceEquals(slot.Target, root))
                {
                    continue;
                }

                object converted;

                if (!TryConvertObjectValue(
                        slot.LastValue,
                        typeof(bool),
                        out converted))
                {
                    throw new InvalidOperationException(
                        "The root ItemTemplate Condition must resolve to a " +
                        "boolean value.");
                }

                if (!(bool)converted)
                    visible = false;
            }

            return visible;
        }

        private void SynchronizeRenderedItemRootCondition(
            Control control)
        {
            if (control == null || control.IsDisposed)
                return;

            ItemsControl host = control.Parent as ItemsControl;

            if (host == null || host.IsDisposed || host.Disposing)
                return;

            RenderedItemRecord record =
                host.FindRenderedItemRecordByRoot(control)
                    as RenderedItemRecord;

            if (record == null)
                return;

            record.RootConditionVisible =
                GetInitialRootConditionVisibility(
                    record.BindingSlots,
                    control);
            record.MeasureCacheValid = false;
            ApplyItemRootVisibility(record);
        }

        private static int FindControlReferenceIndex(
            Control.ControlCollection controls,
            Control target,
            ItemsControl host)
        {
            if (controls == null || target == null)
                return -1;

            int i;

            // ControlCollection ordering APIs can use Control.Equals on the
            // .NET 2.0 path. Callers compare this identity index with the
            // reported index before reordering, so an equal-but-distinct
            // custom Control can never make SetChildIndex move its sibling.
            for (i = 0; i < controls.Count; i++)
            {
                if (host != null)
                    host.RecordItemControlReferenceScanProbe();

                if (Object.ReferenceEquals(controls[i], target))
                    return i;
            }

            return -1;
        }

        private void CommitItemsRefresh(
            ItemsRefreshState state)
        {
            ItemsControl host = state.Host;

            if (host == null ||
                host.IsDisposed ||
                host.PendingRefresh != state ||
                state.Generation != host.RefreshGeneration)
            {
                DisposeUnattachedNewRecords(state);
                return;
            }

            if (state.Timer != null)
            {
                state.Timer.Stop();
                state.Timer.Dispose();
                state.Timer = null;
            }

            ArrayList finalRecords = new ArrayList(
                state.NewRecords.Count);
            int i;

            // If every item kept its existing Control, there is no visual-tree swap to
            // perform. Patches have already been applied progressively. Just publish the
            // new logical order/data and let normal WinForms painting continue.
            if (state.BuildQueue.Count == 0 &&
                state.NewRecords.Count == state.OldRecords.Count)
            {
                for (i = 0; i < state.NewRecords.Count; i++)
                {
                    RenderedItemRecord patchedRecord =
                        state.NewRecords[i] as RenderedItemRecord;

                    if (patchedRecord != null && patchedRecord.Control != null)
                        finalRecords.Add(patchedRecord);
                }

                bool sameControlOrder = HaveSameControlOrder(
                    state.OldRecords,
                    finalRecords);
                bool leavingDirectViewport =
                    host.DirectVirtualActive;

                host.PublishRenderedItemRecords(finalRecords);
                MarkItemsRefreshCommitted(state);

                Exception commitError = null;

                if (leavingDirectViewport)
                {
                    try
                    {
                        CommitNormalRendererFromDirectViewport(host);
                    }
                    catch (Exception ex)
                    {
                        commitError = FirstItemsCommitError(
                            commitError,
                            ex);
                    }
                }

                // A true no-op refresh must remain a no-op visually: do not relayout or
                // invalidate the host unless order or a layout-affecting binding changed.
                if (state.Generation == host.RefreshGeneration &&
                    (leavingDirectViewport ||
                     !sameControlOrder ||
                     state.AnyLayoutChange))
                {
                    try
                    {
                        host.PerformLayout();
                    }
                    catch (Exception ex)
                    {
                        commitError = FirstItemsCommitError(
                            commitError,
                            ex);
                    }

                    if (state.Generation == host.RefreshGeneration)
                    {
                        try
                        {
                            RestoreItemsScrollPosition(
                                host,
                                state.PreviousScrollX,
                                state.PreviousScrollY);
                        }
                        catch (Exception ex)
                        {
                            commitError = FirstItemsCommitError(
                                commitError,
                                ex);
                        }
                    }
                }

                if (state.Generation == host.RefreshGeneration)
                {
                    try
                    {
                        host.RaiseRefreshCompleted();
                    }
                    catch (Exception ex)
                    {
                        commitError = FirstItemsCommitError(
                            commitError,
                            ex);
                    }
                }

                if (commitError != null)
                    throw commitError;

                return;
            }

            for (i = 0; i < state.NewRecords.Count; i++)
            {
                RenderedItemRecord record =
                    (RenderedItemRecord)state.NewRecords[i];

                // Condition=false at the ItemTemplate root intentionally means
                // this data item has no visual record.
                if (record.Control != null)
                    finalRecords.Add(record);
            }

            ArrayList oldToRemove = new ArrayList();

            // An initial publication has no old ownership to compare. Avoid
            // constructing and populating a reference-identity table for every
            // newly built row when the old snapshot is empty.
            if (state.OldRecords.Count > 0)
            {
                Hashtable retainedControls =
                    new Hashtable(
                        finalRecords.Count,
                        _runtimeObjectReferenceComparer);

                for (i = 0; i < finalRecords.Count; i++)
                {
                    RenderedItemRecord record =
                        (RenderedItemRecord)finalRecords[i];

                    retainedControls[record.Control] = true;
                }

                for (i = 0; i < state.OldRecords.Count; i++)
                {
                    RenderedItemRecord oldRecord =
                        state.OldRecords[i] as RenderedItemRecord;

                    if (oldRecord == null ||
                        oldRecord.Control == null)
                    {
                        continue;
                    }

                    if (!retainedControls.ContainsKey(
                            oldRecord.Control))
                    {
                        oldToRemove.Add(oldRecord);
                    }
                }
            }

            host.SuspendLayout();

            try
            {
                // New controls are attached hidden while the previous controls
                // remain visible. This avoids exposing a half-built list.
                for (i = 0; i < finalRecords.Count; i++)
                {
                    RenderedItemRecord record =
                        (RenderedItemRecord)finalRecords[i];

                    if (record.Control.Parent != host)
                    {
                        record.Control.Visible = false;

                        if (host.PendingRefresh != state ||
                            state.Generation != host.RefreshGeneration)
                        {
                            break;
                        }

                        host.Controls.Add(record.Control);

                        if (host.PendingRefresh != state ||
                            state.Generation != host.RefreshGeneration)
                        {
                            break;
                        }
                    }
                }

                host.MoveScrollExtentMarkerBehindItems();

                // Publish a fully visible new tree before committing it.
                // If a VisibleChanged/Z-order callback reenters, cancellation can
                // still discard these controls while the old viewport stays intact.
                for (i = 0; i < finalRecords.Count; i++)
                {
                    RenderedItemRecord record =
                        finalRecords[i] as RenderedItemRecord;

                    if (record == null || record.Control == null)
                        continue;

                    ElementInfo recordInfo = GetInfo(record.Control);
                    record.Control.Visible =
                        record.IntendedVisible &&
                        !recordInfo.Hidden &&
                        !recordInfo.Collapsed;

                    if (host.PendingRefresh != state ||
                        state.Generation != host.RefreshGeneration)
                    {
                        break;
                    }

                    int desiredChildIndex =
                        Math.Min(i, host.Controls.Count - 1);

                    bool alreadyAtDesiredIndex =
                        desiredChildIndex >= 0 &&
                        Object.ReferenceEquals(
                            host.Controls[desiredChildIndex],
                            record.Control);

                    if (!alreadyAtDesiredIndex)
                    {
                        int reportedChildIndex =
                            host.Controls.GetChildIndex(record.Control);
                        bool reportedReferenceCorrect =
                            reportedChildIndex >= 0 &&
                            reportedChildIndex < host.Controls.Count &&
                            Object.ReferenceEquals(
                                host.Controls[reportedChildIndex],
                                record.Control);

                        int referenceChildIndex =
                            reportedReferenceCorrect
                                ? reportedChildIndex
                                : FindControlReferenceIndex(
                                    host.Controls,
                                    record.Control,
                                    host);

                        if (referenceChildIndex >= 0 &&
                            reportedReferenceCorrect &&
                            referenceChildIndex != desiredChildIndex)
                        {
                            host.Controls.SetChildIndex(
                                record.Control,
                                desiredChildIndex);
                        }
                    }

                    if (host.PendingRefresh != state ||
                        state.Generation != host.RefreshGeneration)
                    {
                        break;
                    }
                }
            }
            catch
            {
                host.ResumeLayout(false);
                throw;
            }

            if (host.PendingRefresh != state ||
                state.Generation != host.RefreshGeneration)
            {
                host.ResumeLayout(false);
                DisposeUnattachedNewRecords(state);
                return;
            }

            // The custom layout reads this ordered list, not Controls order.
            bool transitioningFromDirectViewport =
                host.DirectVirtualActive;
            host.PublishRenderedItemRecords(finalRecords);
            MarkItemsRefreshCommitted(state);

            Exception swapError = null;

            if (transitioningFromDirectViewport)
            {
                try
                {
                    CommitNormalRendererFromDirectViewport(host);
                }
                catch (Exception ex)
                {
                    swapError = FirstItemsCommitError(
                        swapError,
                        ex);
                }
            }

            for (i = 0; i < oldToRemove.Count; i++)
            {
                RenderedItemRecord oldRecord =
                    oldToRemove[i] as RenderedItemRecord;
                Control oldControl = oldRecord == null
                    ? null
                    : oldRecord.Control;

                if (oldControl == null || oldControl.IsDisposed)
                    continue;

                try
                {
                    oldControl.Visible = false;
                }
                catch (Exception ex)
                {
                    swapError = FirstItemsCommitError(
                        swapError,
                        ex);
                }
            }

            try
            {
                host.ResumeLayout(false);
            }
            catch (Exception ex)
            {
                swapError = FirstItemsCommitError(
                    swapError,
                    ex);
            }

            if (state.Generation == host.RefreshGeneration)
            {
                try
                {
                    host.PerformLayout();
                }
                catch (Exception ex)
                {
                    swapError = FirstItemsCommitError(
                        swapError,
                        ex);
                }

                if (state.Generation == host.RefreshGeneration)
                {
                    try
                    {
                        RestoreItemsScrollPosition(
                            host,
                            state.PreviousScrollX,
                            state.PreviousScrollY);
                    }
                    catch (Exception ex)
                    {
                        swapError = FirstItemsCommitError(
                            swapError,
                            ex);
                    }
                }
            }

            // Retiring controls can make ScrollableControl recalculate its native
            // range. Append-only and non-scrolling initial commits have nothing to
            // retire, so do not suspend layout for those common paths.
            if (oldToRemove.Count > 0)
            {
                try
                {
                    host.SuspendLayout();
                }
                catch (Exception ex)
                {
                    swapError = FirstItemsCommitError(
                        swapError,
                        ex);
                }

                for (i = 0; i < oldToRemove.Count; i++)
                {
                    RenderedItemRecord oldRecord =
                        oldToRemove[i] as RenderedItemRecord;

                    if (oldRecord == null)
                        continue;

                    try
                    {
                        // Retire the record, not only its native Control. The
                        // record owns item-template binding slots and therefore
                        // the source subscriptions that must be detached when a
                        // row leaves the committed tree.
                        DisposeRenderedItemRecord(oldRecord);
                    }
                    catch (Exception ex)
                    {
                        swapError = FirstItemsCommitError(
                            swapError,
                            ex);
                    }
                }

                try
                {
                    host.ResumeLayout(false);
                }
                catch (Exception ex)
                {
                    swapError = FirstItemsCommitError(
                        swapError,
                        ex);
                }
            }

            bool needsFinalNativeRangeLayout =
                oldToRemove.Count > 0 ||
                (host.AutoScroll &&
                 state.OldRecords.Count == 0 &&
                 finalRecords.Count > 0);

            // Removing old controls can change native bars. A scrolling host's
            // first publication also receives one final pass because legacy
            // ScrollableControl can finish initializing its range only after the
            // first committed child bounds exist. Append-only commits and hosts
            // with AutoScroll disabled are already fully arranged above.
            if (needsFinalNativeRangeLayout &&
                state.Generation == host.RefreshGeneration)
            {
                try
                {
                    host.PerformLayout();
                }
                catch (Exception ex)
                {
                    swapError = FirstItemsCommitError(
                        swapError,
                        ex);
                }

                if (state.Generation == host.RefreshGeneration)
                {
                    try
                    {
                        RestoreItemsScrollPosition(
                            host,
                            state.PreviousScrollX,
                            state.PreviousScrollY);
                    }
                    catch (Exception ex)
                    {
                        swapError = FirstItemsCommitError(
                            swapError,
                            ex);
                    }
                }
            }

            if (state.Generation == host.RefreshGeneration)
            {
                try
                {
                    host.RaiseRefreshCompleted();
                }
                catch (Exception ex)
                {
                    swapError = FirstItemsCommitError(
                        swapError,
                        ex);
                }
            }

            if (swapError != null)
                throw swapError;
        }

        private void FailItemsRefresh(
            ItemsRefreshState state,
            Exception error,
            bool synchronous)
        {
            if (state == null)
                return;

            ItemsControl host = state.Host;

            if (state.Timer != null)
            {
                state.Timer.Stop();
                state.Timer.Dispose();
                state.Timer = null;
            }

            int transitionGeneration = -1;
            bool claimed =
                host != null &&
                !host.IsDisposed &&
                host.PendingRefresh == state;

            if (claimed)
                host.BeginItemsRollback();

            try
            {
                if (claimed)
                {
                    transitionGeneration = ++host.RefreshGeneration;
                    host.PendingRefresh = null;
                    host.SetRefreshing(false, error);
                    RestoreCommittedItemsSource(host);

                }

                Exception rollbackError = RollbackAppliedItemsPatches(
                    state,
                    true,
                    transitionGeneration);

                rollbackError = FirstItemsCommitError(
                    rollbackError,
                    DisposeUnattachedNewRecords(state));

                if (OwnsItemsTransition(host, transitionGeneration))
                {
                    ResumeDirectViewportAfterNormalRollback(
                        host,
                        transitionGeneration);

                    try
                    {
                        RelayoutAfterItemsRollback(
                            host,
                            state,
                            transitionGeneration);
                    }
                    catch (Exception ex)
                    {
                        rollbackError = FirstItemsCommitError(
                            rollbackError,
                            ex);
                    }

                    error = IncludeItemsRollbackError(
                        error,
                        rollbackError);
                    host.SetRefreshing(false, error);

                    try
                    {
                        host.RaiseRefreshFailed();
                    }
                    catch (Exception ex)
                    {
                        error = IncludeItemsRollbackError(
                            error,
                            ex);

                        if (OwnsItemsTransition(
                            host,
                            transitionGeneration))
                        {
                            host.SetRefreshing(false, error);
                        }
                    }
                }
            }
            finally
            {
                if (claimed)
                    host.EndItemsRollback(false);
            }
        }

        private bool CancelItemsRefresh(
            ItemsControl host,
            bool disposing)
        {
            return CancelItemsRefresh(
                host,
                disposing,
                false);
        }

        private bool CancelItemsRefresh(
            ItemsControl host,
            bool disposing,
            bool preserveRequestedState)
        {
            if (host == null)
                return true;

            ItemsRefreshState state =
                host.PendingRefresh as ItemsRefreshState;

            if (state == null)
                return true;

            host.BeginItemsRollback();
            bool deferredRequestStarted = false;
            bool ownsTransition = false;

            try
            {
                int transitionGeneration = ++host.RefreshGeneration;
                host.PendingRefresh = null;

                if (!disposing)
                    host.SetRefreshing(false, null);

                if (!preserveRequestedState)
                    RestoreCommittedItemsSource(host);

                if (state.Timer != null)
                {
                    state.Timer.Stop();
                    state.Timer.Dispose();
                    state.Timer = null;
                }

                Exception cancellationError = RollbackAppliedItemsPatches(
                    state,
                    !disposing,
                    transitionGeneration);

                cancellationError = FirstItemsCommitError(
                    cancellationError,
                    DisposeUnattachedNewRecords(state));

                if (!disposing &&
                    OwnsItemsTransition(host, transitionGeneration))
                {
                    ResumeDirectViewportAfterNormalRollback(
                        host,
                        transitionGeneration);

                    try
                    {
                        RelayoutAfterItemsRollback(
                            host,
                            state,
                            transitionGeneration);
                    }
                    catch (Exception ex)
                    {
                        cancellationError = FirstItemsCommitError(
                            cancellationError,
                            ex);
                    }
                }

                if (cancellationError != null &&
                    preserveRequestedState &&
                    OwnsItemsTransition(host, transitionGeneration))
                {
                    RestoreCommittedItemsSource(host);
                }

                if (!disposing &&
                    OwnsItemsTransition(host, transitionGeneration))
                {
                    host.SetRefreshing(false, cancellationError);
                }

                if (cancellationError != null &&
                    !disposing &&
                    OwnsItemsTransition(host, transitionGeneration))
                {
                    throw new InvalidOperationException(
                        "The item refresh was canceled, but its in-progress visual " +
                        "changes could not be fully cleaned up: " +
                        cancellationError.Message,
                        cancellationError);
                }

                ownsTransition = OwnsItemsTransition(
                    host,
                    transitionGeneration);
            }
            finally
            {
                deferredRequestStarted =
                    host.EndItemsRollback(disposing);
            }

            return !deferredRequestStarted && ownsTransition;
        }

        private static bool OwnsItemsTransition(
            ItemsControl host,
            int transitionGeneration)
        {
            return host != null &&
                   !host.IsDisposed &&
                   transitionGeneration >= 0 &&
                   host.RefreshGeneration == transitionGeneration &&
                   host.PendingRefresh == null;
        }

        private void RelayoutAfterItemsRollback(
            ItemsControl host,
            ItemsRefreshState state,
            int transitionGeneration)
        {
            if (host == null ||
                host.IsDisposed ||
                state == null ||
                !OwnsItemsTransition(host, transitionGeneration))
            {
                return;
            }

            host.PerformLayout();

            if (!OwnsItemsTransition(host, transitionGeneration))
                return;

            RestoreItemsScrollPosition(
                host,
                state.RollbackScrollX,
                state.RollbackScrollY);

            if (!OwnsItemsTransition(host, transitionGeneration))
                return;

            host.Invalidate(false);
        }

        private void MarkItemsRefreshCommitted(
            ItemsRefreshState state)
        {
            if (state == null || state.Host == null)
                return;

            ItemsControl host = state.Host;

            CommitItemsSource(host);
            state.Committed = true;
            host.PendingRefresh = null;
            host.SetRefreshing(false, null);
            RefreshItemsControlPresetIndex(host, true);
        }

        private static Exception FirstItemsCommitError(
            Exception current,
            Exception next)
        {
            return current == null ? next : current;
        }

        private static Exception IncludeItemsRollbackError(
            Exception primary,
            Exception rollbackError)
        {
            if (rollbackError == null)
                return primary;

            return new InvalidOperationException(
                "The item refresh failed and its in-progress visual changes " +
                "could not be fully cleaned up: " + rollbackError.Message,
                primary == null ? rollbackError : primary);
        }

        private static void CommitItemsSource(ItemsControl host)
        {
            if (host == null)
                return;

            host.CommittedItemSource = host.ItemSource;
            host.CommittedItemValues = host.ItemValues;
        }

        private static void RestoreCommittedItemsSource(ItemsControl host)
        {
            if (host == null)
                return;

            host.ItemSource = host.CommittedItemSource;
            host.ItemValues = host.CommittedItemValues;
        }

        private Exception DisposeUnattachedNewRecords(
            ItemsRefreshState state)
        {
            if (state == null || state.NewRecords == null)
                return null;

            ItemsControl host = state.Host;
            Hashtable protectedControls =
                new Hashtable(_runtimeObjectReferenceComparer);
            Hashtable disposedControls =
                new Hashtable(_runtimeObjectReferenceComparer);
            Exception firstError = null;
            int i;

            // Controls that belonged to the committed tree before this refresh are never
            // owned by the cancelled state. Progressive construction remains detached
            // until the complete transaction can be published atomically.
            if (state.OldRecords != null)
            {
                for (i = 0; i < state.OldRecords.Count; i++)
                {
                    RenderedItemRecord oldRecord =
                        state.OldRecords[i] as RenderedItemRecord;

                    if (oldRecord != null && oldRecord.Control != null)
                        protectedControls[oldRecord.Control] = true;
                }
            }

            if (host != null && host.RenderedItems != null)
            {
                for (i = 0; i < host.RenderedItems.Count; i++)
                {
                    RenderedItemRecord currentRecord =
                        host.RenderedItems[i] as RenderedItemRecord;

                    if (currentRecord != null && currentRecord.Control != null)
                        protectedControls[currentRecord.Control] = true;
                }
            }

            bool suspended = host != null && !host.IsDisposed;

            if (suspended)
            {
                try
                {
                    host.SuspendLayout();
                }
                catch (Exception ex)
                {
                    suspended = false;
                    firstError = ex;
                }
            }

            for (i = 0; i < state.NewRecords.Count; i++)
            {
                RenderedItemRecord record =
                    state.NewRecords[i] as RenderedItemRecord;

                if (record == null)
                    continue;

                if (record.Control == null)
                {
                    ArrayList emptyRecordSlots = record.BindingSlots;
                    record.BindingSlots = null;
                    firstError = FirstItemsCommitError(
                        firstError,
                        ReleaseRenderBindingSlots(emptyRecordSlots));
                    record.Item = null;
                    record.FunctionResults = null;
                    record.VersionValue = null;
                    continue;
                }

                if (protectedControls.ContainsKey(record.Control) ||
                    disposedControls.ContainsKey(record.Control))
                {
                    continue;
                }

                Control control = record.Control;
                disposedControls[control] = true;
                record.Control = null;
                ArrayList bindingSlots = record.BindingSlots;
                record.BindingSlots = null;
                firstError = FirstItemsCommitError(
                    firstError,
                    ReleaseRenderBindingSlots(bindingSlots));
                record.Item = null;
                record.FunctionResults = null;
                record.VersionValue = null;

                // A staged control may already be parented when cancellation
                // begins. Remove it before disposal so it cannot remain as an
                // invisible orphan in the committed host.
                try
                {
                    if (control.Parent != null)
                        control.Parent.Controls.Remove(control);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(
                        firstError,
                        ex);
                }

                try
                {
                    ReleaseElementTree(control);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(
                        firstError,
                        ex);
                }

                try
                {
                    control.Dispose();
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(
                        firstError,
                        ex);
                }
            }

            if (suspended)
            {
                try
                {
                    host.ResumeLayout(false);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(
                        firstError,
                        ex);
                }
            }

            return firstError;
        }
    }
}
