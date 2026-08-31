using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private const int DirectVirtualLinearRetirementScanLimit = 64;

        private sealed class SynchronousVirtualRealization
        {
            public ItemsControl Host;
            public int ExpectedGeneration;
            public ArrayList SourceValues;
            public ArrayList OriginalRenderedItems;
            public ArrayList OriginalRecords;
            public int OriginalRecordCursor;
            public ArrayList CacheRecords;
            public ArrayList Candidates;
            public bool LayoutSuspended;
            public bool Published;
        }

        private sealed class SynchronousVirtualCandidate
        {
            public RenderedItemRecord Record;
            public int DesiredIndex;
            public bool ReusedCurrent;
            public bool BorrowedCache;
            public int OriginalCacheIndex;
            public bool CacheWasParented;
            public bool AttachedByRealization;
            public bool BindingSlotsActivated;
            public bool CrossItemRecycled;
            public ItemPatchPlan AppliedPatch;
            public bool DesiredHasVersionValue;
            public object DesiredVersionValue;
            public bool InvalidateMeasureCache;
        }

        private sealed class SynchronousVirtualSupersededException
            : Exception
        {
        }

        private sealed class SynchronousVirtualCleanupException
            : Exception
        {
            private readonly Exception[] _cleanupErrors;

            public SynchronousVirtualCleanupException(
                string message,
                Exception innerException,
                Exception[] cleanupErrors)
                : base(message, innerException)
            {
                _cleanupErrors = cleanupErrors;
            }

            public Exception[] CleanupErrors
            {
                get { return _cleanupErrors; }
            }
        }

        private struct SynchronousVirtualCleanupErrors
        {
            private ArrayList _items;

            public int Count
            {
                get { return _items == null ? 0 : _items.Count; }
            }

            public Exception First
            {
                get
                {
                    return _items == null || _items.Count == 0
                        ? null
                        : _items[0] as Exception;
                }
            }

            public void Add(Exception error)
            {
                if (error == null)
                    return;

                if (_items == null)
                    _items = new ArrayList();

                _items.Add(error);
            }

            public void CopyTo(Exception[] target)
            {
                if (_items != null)
                    _items.CopyTo(target);
            }
        }

        /// <summary>
        /// Reconciles exactly one bounded logical data range on the calling UI
        /// thread. The caller owns source/model preparation, refresh events,
        /// layout, and generation allocation.
        /// </summary>
        private bool ReconcileVirtualRangeSynchronously(
            ItemsControl host,
            int start,
            int end,
            bool forceRebuild,
            bool validateValues,
            int expectedGeneration)
        {
            return ReconcileVirtualRangeSynchronouslyCore(
                host,
                start,
                end,
                forceRebuild,
                validateValues,
                false,
                expectedGeneration);
        }

        private bool ReconcileVirtualRangeWithPatchesSynchronously(
            ItemsControl host,
            int start,
            int end,
            bool forceRebuild,
            int expectedGeneration)
        {
            return ReconcileVirtualRangeSynchronouslyCore(
                host,
                start,
                end,
                forceRebuild,
                false,
                true,
                expectedGeneration);
        }

        private bool ReconcileVirtualRangeSynchronouslyCore(
            ItemsControl host,
            int start,
            int end,
            bool forceRebuild,
            bool validateValues,
            bool patchValues,
            int expectedGeneration)
        {
            ValidateSynchronousVirtualRange(
                host,
                start,
                end);

            if (!IsSynchronousVirtualHostCurrent(
                    host,
                    expectedGeneration))
            {
                return false;
            }

            SynchronousVirtualRealization realization =
                new SynchronousVirtualRealization();

            realization.Host = host;
            realization.ExpectedGeneration = expectedGeneration;
            realization.SourceValues = host.ItemValues;
            realization.OriginalRenderedItems = host.RenderedItems;
            // The direct viewport publishes RenderedItems as an immutable
            // snapshot: a later direct commit replaces the list instead of
            // editing it. Retain that snapshot directly; cloning it only
            // duplicated the visible-range backing array before any
            // callback-capable work began.
            realization.OriginalRecords = host.RenderedItems;
            realization.OriginalRecordCursor = 0;
            ArrayList cacheRecords = host.DirectVirtualCacheRecords;

            if (cacheRecords == null)
            {
                cacheRecords = new ArrayList();
                host.DirectVirtualCacheRecords = cacheRecords;
            }

            realization.CacheRecords = cacheRecords;
            realization.Candidates = new ArrayList(
                end < start ? 0 : end - start + 1);

            Exception failure = null;
            SynchronousVirtualCleanupErrors cleanupErrors =
                new SynchronousVirtualCleanupErrors();

            try
            {
                int index;

                for (index = Math.Max(0, start); index <= end; index++)
                {
                    EnsureSynchronousVirtualRealizationCurrent(
                        realization);

                    // Logical row indices map directly to the matching item
                    // slots; no parallel per-item metadata list is required.
                    object item = realization.SourceValues[index];
                    string key = GetStableItemKey(
                        host,
                        item,
                        index);

                    EnsureSynchronousVirtualRealizationCurrent(
                        realization);

                    SynchronousVirtualCandidate candidate =
                        TryReuseCurrentVirtualRecord(
                            realization,
                            item,
                            key,
                            index,
                            forceRebuild,
                            validateValues,
                            patchValues);

                    if (candidate == null)
                    {
                        candidate = TryBorrowVirtualCacheRecord(
                            realization,
                            item,
                            key,
                            index,
                            forceRebuild,
                            validateValues || patchValues);
                    }

                    if (candidate == null)
                    {
                        candidate = BuildSynchronousVirtualCandidate(
                            realization,
                            item,
                            key,
                            index);
                    }

                    if (candidate != null)
                        realization.Candidates.Add(candidate);
                }

                RejectCollapsedSynchronousVirtualRoots(realization);
                StageSynchronousVirtualControls(realization);
                PublishSynchronousVirtualRange(realization);
            }
            catch (SynchronousVirtualSupersededException)
            {
                CleanupUnpublishedSynchronousVirtualRealization(
                    realization,
                    ref cleanupErrors);

                if (cleanupErrors.Count > 0)
                {
                    throw CreateSynchronousVirtualFailure(
                        null,
                        cleanupErrors,
                        false);
                }

                return false;
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            if (!realization.Published)
            {
                CleanupUnpublishedSynchronousVirtualRealization(
                    realization,
                    ref cleanupErrors);

                throw CreateSynchronousVirtualFailure(
                    failure,
                    cleanupErrors,
                    false);
            }

            RetireLeavingSynchronousVirtualRecords(
                realization,
                forceRebuild,
                validateValues || patchValues,
                ref cleanupErrors);

            ResumeSynchronousVirtualLayout(
                realization,
                ref cleanupErrors);

            if (failure != null || cleanupErrors.Count > 0)
            {
                throw new ItemsRefreshCommittedException(
                    CreateSynchronousVirtualFailure(
                        failure,
                        cleanupErrors,
                        true));
            }

            return true;
        }

        private void ValidateSynchronousVirtualRange(
            ItemsControl host,
            int start,
            int end)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            if (!Object.ReferenceEquals(host.Runtime, this))
            {
                throw new InvalidOperationException(
                    "The ItemsControl belongs to a different XamlRuntime.");
            }

            if (host.IsDisposed || IsDisposed)
            {
                throw new ObjectDisposedException(
                    host.IsDisposed ? "host" : "XamlRuntime");
            }

            ArrayList values = host.ItemValues;

            if (values == null)
            {
                throw new InvalidOperationException(
                    "The ItemsControl does not have a logical item snapshot.");
            }

            if (start == -1 && end == -1)
                return;

            if (start < 0 || start >= values.Count)
                throw new ArgumentOutOfRangeException("start");

            if (end < start || end >= values.Count)
                throw new ArgumentOutOfRangeException("end");

            if (host.TemplateRoot == null)
            {
                throw new InvalidOperationException(
                    "ItemsControl has items but no ItemsControl.ItemTemplate.");
            }
        }

        private static bool IsSynchronousVirtualHostCurrent(
            ItemsControl host,
            int expectedGeneration)
        {
            return host != null &&
                   !host.IsDisposed &&
                   !host.DirectVirtualDisposed &&
                   host.DirectVirtualActive &&
                   host.DirectVirtualGeneration == expectedGeneration &&
                   host.RefreshGeneration == expectedGeneration;
        }

        private static void EnsureSynchronousVirtualRealizationCurrent(
            SynchronousVirtualRealization realization)
        {
            if (realization == null ||
                !IsSynchronousVirtualHostCurrent(
                    realization.Host,
                    realization.ExpectedGeneration) ||
                !Object.ReferenceEquals(
                    realization.SourceValues,
                    realization.Host.ItemValues) ||
                !Object.ReferenceEquals(
                    realization.OriginalRenderedItems,
                    realization.Host.RenderedItems) ||
                !Object.ReferenceEquals(
                    realization.CacheRecords,
                    realization.Host.DirectVirtualCacheRecords))
            {
                throw new SynchronousVirtualSupersededException();
            }
        }

        private SynchronousVirtualCandidate
            TryReuseCurrentVirtualRecord(
                SynchronousVirtualRealization realization,
                object item,
                string key,
                int index,
                bool forceRebuild,
                bool validateValues,
                bool patchValues)
        {
            ItemsControl host = realization.Host;

            if (forceRebuild || validateValues || !host.ReuseItems)
                return null;

            while (realization.OriginalRecordCursor <
                realization.OriginalRecords.Count)
            {
                RenderedItemRecord record =
                    realization.OriginalRecords[
                        realization.OriginalRecordCursor] as
                            RenderedItemRecord;

                if (record == null || record.LogicalIndex < index)
                {
                    realization.OriginalRecordCursor++;
                    continue;
                }

                // Both the desired viewport and the published record snapshot
                // are ordered by logical index. A later record cannot match this
                // desired slot; retain it for the next desired index.
                if (record.LogicalIndex > index)
                    return null;

                realization.OriginalRecordCursor++;
                bool pendingReactivePatch =
                    IsPendingReactiveItemPatchOwner(
                        host,
                        record);

                if (!CanReuseSynchronousVirtualRecord(
                        host,
                        record,
                        item,
                        key,
                        index,
                        true,
                        pendingReactivePatch))
                {
                    return null;
                }

                SynchronousVirtualCandidate candidate =
                    new SynchronousVirtualCandidate();

                candidate.Record = record;
                candidate.DesiredIndex = index;
                candidate.ReusedCurrent = true;
                candidate.OriginalCacheIndex = -1;

                candidate.DesiredVersionValue =
                    GetDirectVirtualItemVersion(
                        host,
                        item,
                        index,
                        out candidate.DesiredHasVersionValue);

                EnsureSynchronousVirtualRealizationCurrent(realization);

                if (!patchValues)
                    return candidate;

                bool versionUnchanged =
                    candidate.DesiredHasVersionValue &&
                    record.HasVersionValue &&
                    AreFunctionResultsEquivalent(
                        record.VersionValue,
                        candidate.DesiredVersionValue);

                if (versionUnchanged &&
                    !host.ReevaluateFunctionsOnRefresh &&
                    !RenderedItemRecordRequiresReactiveValidation(
                        record,
                        item))
                {
                    return candidate;
                }

                ItemPatchPlan patch = CreateItemPatchPlan(
                    host,
                    record,
                    item,
                    versionUnchanged);

                EnsureSynchronousVirtualRealizationCurrent(realization);

                if (patch.RequiresRebuild)
                    return null;

                candidate.AppliedPatch = patch;
                candidate.InvalidateMeasureCache = patch.AffectsLayout;

                // Publish the candidate as the rollback owner before the first
                // WinForms property setter can invoke application code.
                realization.Candidates.Add(candidate);

                if (!ApplyItemPatchPlan(
                        null,
                        patch,
                        host,
                        realization.ExpectedGeneration))
                {
                    throw new SynchronousVirtualSupersededException();
                }

                EnsureSynchronousVirtualRealizationCurrent(realization);
                realization.Candidates.Remove(candidate);
                return candidate;
            }

            return null;
        }

        private SynchronousVirtualCandidate
            TryBorrowVirtualCacheRecord(
                SynchronousVirtualRealization realization,
                object item,
                string key,
                int index,
                bool forceRebuild,
                bool validateValues)
        {
            ItemsControl host = realization.Host;
            ArrayList cache = realization.CacheRecords;

            if (forceRebuild || validateValues || !host.ReuseItems ||
                cache == null || cache.Count == 0 ||
                host.VirtualizationCacheItems <= 0)
            {
                return null;
            }

            int minimum = Math.Max(
                0,
                cache.Count - host.VirtualizationCacheItems);
            int i;

            for (i = cache.Count - 1; i >= minimum; i--)
            {
                RenderedItemRecord record =
                    cache[i] as RenderedItemRecord;

                if (!CanReuseSynchronousVirtualRecord(
                        host,
                        record,
                        item,
                        key,
                        index,
                        false,
                        false))
                {
                    continue;
                }

                SynchronousVirtualCandidate candidate =
                    new SynchronousVirtualCandidate();

                candidate.Record = record;
                candidate.DesiredIndex = index;
                candidate.BorrowedCache = true;
                candidate.OriginalCacheIndex = i;
                candidate.CacheWasParented =
                    Object.ReferenceEquals(record.Control.Parent, host);

                // Transfer the hint into this staging operation before any
                // binding callback can reenter and inspect the cache.
                cache.RemoveAt(i);
                realization.Candidates.Add(candidate);

                // Activation is transactional, but its own rollback can report
                // a retryable detach failure. Mark the attempt before entering
                // it so unpublished cleanup always retries deactivation instead
                // of returning a partially active record to the cache.
                candidate.BindingSlotsActivated = true;
                ActivateRenderedItemRecordBindings(
                    record,
                    host,
                    item);
                EnsureSynchronousVirtualRealizationCurrent(realization);

                if (RenderedItemRecordHasReactiveDirtySlot(record))
                {
                    // A cache entry is only a construction hint.  If binding
                    // activation discovers a structural change, discard the
                    // old tree and let the normal compiler build this row.
                    // Keep the candidate registered until disposal succeeds
                    // so failure cleanup always has exactly one owner.
                    candidate.BorrowedCache = false;
                    DisposeRenderedItemRecord(record);
                    EnsureSynchronousVirtualRealizationCurrent(realization);
                    realization.Candidates.Remove(candidate);
                    return null;
                }

                realization.Candidates.Remove(candidate);
                return candidate;
            }

            if (host.ItemRecycling != ItemRecyclingMode.Explicit)
                return null;

            return TryBorrowExplicitCrossItemVirtualCacheRecord(
                realization,
                item,
                key,
                index,
                minimum);
        }

        private static bool CanReuseSynchronousVirtualRecord(
            ItemsControl host,
            RenderedItemRecord record,
            object item,
            string key,
            int desiredIndex,
            bool requireSameIndex,
            bool allowReactiveDirty)
        {
            if (record == null || record.Control == null ||
                record.Control.IsDisposed || record.Control.Disposing ||
                !Object.ReferenceEquals(record.Item, item) ||
                !String.Equals(
                    record.Key,
                    key,
                    StringComparison.Ordinal) ||
                (!allowReactiveDirty &&
                 RenderedItemRecordHasReactiveDirtySlot(record)))
            {
                return false;
            }

            if (requireSameIndex &&
                (record.LogicalIndex != desiredIndex ||
                 !Object.ReferenceEquals(record.Control.Parent, host)))
            {
                return false;
            }

            if (!requireSameIndex &&
                record.Control.Parent != null &&
                !Object.ReferenceEquals(record.Control.Parent, host))
            {
                return false;
            }

            return true;
        }

        private SynchronousVirtualCandidate
            BuildSynchronousVirtualCandidate(
                SynchronousVirtualRealization realization,
                object item,
                string key,
                int index)
        {
            ItemsControl host = realization.Host;
            RenderedItemRecord record = new RenderedItemRecord();

            record.Owner = host;
            record.Key = key;
            record.Item = item;
            record.FunctionResults = new Hashtable();
            record.BindingSlots = null;
            record.Control = null;
            record.IntendedVisible = true;
            record.RootVisibility = ItemVisibilityState.Visible;
            record.RootConditionVisible = true;
            record.Reused = false;
            record.LogicalIndex = index;
            record.RealizationGeneration =
                host.DirectVirtualGeneration;
            record.MeasureCacheValid = false;
            record.MeasureProposedWidth = 0;
            record.MeasureProposedHeight = 0;
            record.MeasureCachedSize = Size.Empty;
            record.VersionValue = GetDirectVirtualItemVersion(
                host,
                item,
                index,
                out record.HasVersionValue);

            EnsureSynchronousVirtualRealizationCurrent(realization);

            ArrayList bindingSlots;
            Control child = BuildTemplateControl(
                host,
                host.TemplateRoot,
                item,
                record.FunctionResults,
                out bindingSlots);

            if (child == null)
                return null;

            record.Control = child;
            record.BindingSlots = bindingSlots;

            SynchronousVirtualCandidate candidate =
                new SynchronousVirtualCandidate();

            candidate.Record = record;
            candidate.DesiredIndex = index;
            candidate.OriginalCacheIndex = -1;
            realization.Candidates.Add(candidate);

            EnsureSynchronousVirtualRealizationCurrent(realization);

            ActivateRenderedItemRecordBindings(
                record,
                host,
                item);
            candidate.BindingSlotsActivated = true;
            EnsureSynchronousVirtualRealizationCurrent(realization);

            ApplyInheritedProperties(child, host);
            EnsureSynchronousVirtualRealizationCurrent(realization);

            PerformLayoutRecursive(child);
            EnsureSynchronousVirtualRealizationCurrent(realization);

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
            EnsureSynchronousVirtualRealizationCurrent(realization);

            // The caller adds returned candidates. It was published early only
            // so every post-build failure has one cleanup owner.
            realization.Candidates.Remove(candidate);
            return candidate;
        }

        private void StageSynchronousVirtualControls(
            SynchronousVirtualRealization realization)
        {
            EnsureSynchronousVirtualRealizationCurrent(realization);

            realization.Host.SuspendLayout();
            realization.LayoutSuspended = true;

            int i;

            for (i = 0; i < realization.Candidates.Count; i++)
            {
                SynchronousVirtualCandidate candidate =
                    realization.Candidates[i] as
                        SynchronousVirtualCandidate;

                if (candidate == null || candidate.ReusedCurrent)
                    continue;

                Control control = candidate.Record.Control;
                control.Visible = false;
                EnsureSynchronousVirtualRealizationCurrent(realization);

                if (control.Parent == null)
                {
                    realization.Host.Controls.Add(control);
                    candidate.AttachedByRealization = true;
                    EnsureSynchronousVirtualRealizationCurrent(realization);
                }
                else if (!Object.ReferenceEquals(
                             control.Parent,
                             realization.Host))
                {
                    throw new InvalidOperationException(
                        "A staged virtual item is parented by another control.");
                }
            }
        }

        /// <summary>
        /// Verifies the layout-membership invariant after the complete runtime
        /// style/binding/component pipeline has configured each root, but before
        /// controls or RenderedItems are published. This catches implicit and
        /// named styles that static XML eligibility cannot safely duplicate.
        /// </summary>
        private void RejectCollapsedSynchronousVirtualRoots(
            SynchronousVirtualRealization realization)
        {
            EnsureSynchronousVirtualRealizationCurrent(realization);

            int i;

            for (i = 0; i < realization.Candidates.Count; i++)
            {
                SynchronousVirtualCandidate candidate =
                    realization.Candidates[i] as
                        SynchronousVirtualCandidate;

                if (candidate == null ||
                    candidate.Record == null ||
                    candidate.Record.Control == null)
                {
                    continue;
                }

                ElementInfo info = GetInfo(candidate.Record.Control);

                if (info.Collapsed)
                    throw new DirectVirtualizationIneligibleException();

                EnsureSynchronousVirtualRealizationCurrent(realization);
            }
        }

        private void RejectCollapsedPublishedDirectVirtualRoots(
            ItemsControl host)
        {
            if (host == null || host.RenderedItems == null)
                return;

            int i;

            for (i = 0; i < host.RenderedItems.Count; i++)
            {
                RenderedItemRecord record =
                    host.RenderedItems[i] as RenderedItemRecord;

                if (record == null ||
                    record.Control == null ||
                    record.Control.IsDisposed)
                {
                    continue;
                }

                if (GetInfo(record.Control).Collapsed)
                    throw new DirectVirtualizationIneligibleException();
            }
        }

        private void PublishSynchronousVirtualRange(
            SynchronousVirtualRealization realization)
        {
            EnsureSynchronousVirtualRealizationCurrent(realization);

            ArrayList finalRecords = new ArrayList(
                realization.Candidates.Count);
            int retainedReuseCount = 0;
            int cacheReuseCount = 0;
            int crossItemRecycleCount = 0;
            int createdCount = 0;
            int i;

            for (i = 0; i < realization.Candidates.Count; i++)
            {
                SynchronousVirtualCandidate candidate =
                    realization.Candidates[i] as
                        SynchronousVirtualCandidate;

                if (candidate == null || candidate.Record == null ||
                    candidate.Record.Control == null)
                {
                    continue;
                }

                if (!candidate.ReusedCurrent)
                {
                    ApplyItemRootVisibility(candidate.Record);
                    EnsureSynchronousVirtualRealizationCurrent(realization);
                }

                finalRecords.Add(candidate.Record);
            }

            // Candidate construction iterates the requested logical range in
            // ascending order. Publish index fields last so ordinary same-item
            // cache records retain their previous metadata on an earlier fault.
            // Cross-item candidates are disposal-only after reset and already
            // carry their new key/index while staged.
            for (i = 0; i < realization.Candidates.Count; i++)
            {
                SynchronousVirtualCandidate candidate =
                    realization.Candidates[i] as
                        SynchronousVirtualCandidate;

                if (candidate == null || candidate.Record == null)
                    continue;

                candidate.Record.LogicalIndex = candidate.DesiredIndex;
                candidate.Record.RealizationGeneration =
                    realization.ExpectedGeneration;
                candidate.Record.Reused =
                    candidate.ReusedCurrent || candidate.BorrowedCache;

                if (candidate.ReusedCurrent)
                {
                    candidate.Record.HasVersionValue =
                        candidate.DesiredHasVersionValue;
                    candidate.Record.VersionValue =
                        candidate.DesiredVersionValue;

                    if (candidate.InvalidateMeasureCache)
                    {
                        candidate.Record.MeasureCacheValid = false;
                        candidate.Record.MeasureProposedWidth = 0;
                        candidate.Record.MeasureProposedHeight = 0;
                        candidate.Record.MeasureCachedSize = Size.Empty;
                    }
                }

                if (candidate.ReusedCurrent)
                    retainedReuseCount++;
                else if (candidate.BorrowedCache)
                {
                    cacheReuseCount++;
                    if (candidate.CrossItemRecycled)
                        crossItemRecycleCount++;
                }
                else if (candidate.Record.Control != null)
                    createdCount++;
            }

            realization.Host.PublishRenderedItemRecords(finalRecords);
            realization.Published = true;
            UpdateDirectVirtualRealizedRangeFromRecords(
                realization.Host,
                finalRecords);
            RecordDirectVirtualPublishedScrollAxis(realization.Host);
            realization.Host.RecordVirtualRealization(
                retainedReuseCount,
                cacheReuseCount,
                createdCount);
            realization.Host.RecordVirtualCrossItemRecycleSuccess(
                crossItemRecycleCount);
        }

        private void RetireLeavingSynchronousVirtualRecords(
            SynchronousVirtualRealization realization,
            bool forceRebuild,
            bool validateValues,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            Hashtable retained = null;
            Hashtable retainedKeys = null;
            int i;

            // The realized viewport is normally small. Linear scans avoid two
            // hash tables and their bucket arrays on every range change. Keep
            // the indexed fallback for deliberately large overscan windows.
            if (realization.Candidates.Count >
                DirectVirtualLinearRetirementScanLimit)
            {
                retained = new Hashtable(
                    _runtimeObjectReferenceComparer);
                retainedKeys = new Hashtable(
                    StringComparer.Ordinal);

                for (i = 0; i < realization.Candidates.Count; i++)
                {
                    SynchronousVirtualCandidate candidate =
                        realization.Candidates[i] as
                            SynchronousVirtualCandidate;

                    if (candidate == null || candidate.Record == null)
                        continue;

                    if (candidate.Record.Control != null)
                        retained[candidate.Record.Control] = true;

                    if (candidate.Record.Key != null)
                        retainedKeys[candidate.Record.Key] = true;
                }
            }

            for (i = 0; i < realization.OriginalRecords.Count; i++)
            {
                RenderedItemRecord record =
                    realization.OriginalRecords[i] as RenderedItemRecord;

                if (record == null || record.Control == null ||
                    IsSynchronousVirtualCandidateControlRetained(
                        realization.Candidates,
                        retained,
                        record.Control))
                {
                    continue;
                }

                bool cache =
                    !forceRebuild &&
                    !validateValues &&
                    realization.Host.ReuseItems &&
                    realization.Host.RefreshGeneration ==
                        realization.ExpectedGeneration &&
                    realization.Host.VirtualizationCacheItems > 0 &&
                    !IsSynchronousVirtualCandidateKeyRetained(
                        realization.Candidates,
                        retainedKeys,
                        record.Key);

                if (cache)
                {
                    CacheLeavingSynchronousVirtualRecord(
                        realization,
                        record,
                        ref cleanupErrors);
                }
                else
                {
                    DisposeSynchronousVirtualRecord(
                        realization.Host,
                        record,
                        ref cleanupErrors);
                }
            }

            if ((forceRebuild ||
                 validateValues ||
                 !realization.Host.ReuseItems) &&
                realization.Host.RefreshGeneration ==
                    realization.ExpectedGeneration)
            {
                DisposeSynchronousVirtualCache(
                    realization,
                    ref cleanupErrors);
            }

            TrimSynchronousVirtualCache(
                realization,
                ref cleanupErrors);
        }

        private static bool
            IsSynchronousVirtualCandidateControlRetained(
                ArrayList candidates,
                Hashtable retained,
                Control control)
        {
            if (retained != null)
                return retained.ContainsKey(control);

            int i;

            for (i = 0; candidates != null && i < candidates.Count; i++)
            {
                SynchronousVirtualCandidate candidate =
                    candidates[i] as SynchronousVirtualCandidate;

                if (candidate != null && candidate.Record != null &&
                    Object.ReferenceEquals(
                        candidate.Record.Control,
                        control))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSynchronousVirtualCandidateKeyRetained(
            ArrayList candidates,
            Hashtable retainedKeys,
            string key)
        {
            if (key == null)
                return false;

            if (retainedKeys != null)
                return retainedKeys.ContainsKey(key);

            int i;

            for (i = 0; candidates != null && i < candidates.Count; i++)
            {
                SynchronousVirtualCandidate candidate =
                    candidates[i] as SynchronousVirtualCandidate;

                if (candidate != null && candidate.Record != null &&
                    String.Equals(
                        candidate.Record.Key,
                        key,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void CacheLeavingSynchronousVirtualRecord(
            SynchronousVirtualRealization realization,
            RenderedItemRecord record,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            bool ready = true;

            try
            {
                record.Control.Visible = false;
                // Preserve the measured size while detached. Explicit template
                // Width/Height values are reused when this row re-enters the
                // viewport; clearing Bounds would turn them into zero-sized
                // rows and leave gaps after fast scrolling.
                DeactivateRenderBindingSlots(record.BindingSlots);

                // Cached rows are construction hints, not realized children.
                // Remove them from the host so native layout, z-order scans,
                // accessibility traversal, and child-window bookkeeping stay
                // proportional to the visible range. ControlCollection.Remove
                // transfers parent ownership without disposing the control.
                if (record.Control.Parent != null)
                    record.Control.Parent.Controls.Remove(record.Control);
            }
            catch (Exception ex)
            {
                AddSynchronousVirtualCleanupError(
                    ref cleanupErrors,
                    ex);
                ready = false;
            }

            if (!ready ||
                realization.Host.RefreshGeneration !=
                    realization.ExpectedGeneration ||
                !Object.ReferenceEquals(
                    realization.CacheRecords,
                    realization.Host.DirectVirtualCacheRecords))
            {
                DisposeSynchronousVirtualRecord(
                    realization.Host,
                    record,
                    ref cleanupErrors);
                return;
            }

            int i;

            for (i = realization.CacheRecords.Count - 1; i >= 0; i--)
            {
                if (realization.Host.RefreshGeneration !=
                        realization.ExpectedGeneration ||
                    !Object.ReferenceEquals(
                        realization.CacheRecords,
                        realization.Host.DirectVirtualCacheRecords))
                {
                    DisposeSynchronousVirtualRecord(
                        realization.Host,
                        record,
                        ref cleanupErrors);
                    return;
                }

                RenderedItemRecord duplicate =
                    realization.CacheRecords[i] as RenderedItemRecord;

                if (duplicate == null ||
                    !String.Equals(
                        duplicate.Key,
                        record.Key,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                realization.CacheRecords.RemoveAt(i);
                DisposeSynchronousVirtualRecord(
                    realization.Host,
                    duplicate,
                    ref cleanupErrors);
            }

            if (realization.Host.RefreshGeneration !=
                    realization.ExpectedGeneration ||
                !Object.ReferenceEquals(
                    realization.CacheRecords,
                    realization.Host.DirectVirtualCacheRecords))
            {
                DisposeSynchronousVirtualRecord(
                    realization.Host,
                    record,
                    ref cleanupErrors);
                return;
            }

            realization.CacheRecords.Add(record);
        }

        private void DisposeSynchronousVirtualCache(
            SynchronousVirtualRealization realization,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            while (realization.CacheRecords != null &&
                   realization.CacheRecords.Count > 0 &&
                   realization.Host.RefreshGeneration ==
                       realization.ExpectedGeneration &&
                   Object.ReferenceEquals(
                       realization.CacheRecords,
                       realization.Host.DirectVirtualCacheRecords))
            {
                RenderedItemRecord record =
                    realization.CacheRecords[
                        realization.CacheRecords.Count - 1] as
                            RenderedItemRecord;

                realization.CacheRecords.RemoveAt(
                    realization.CacheRecords.Count - 1);
                DisposeSynchronousVirtualRecord(
                    realization.Host,
                    record,
                    ref cleanupErrors);
            }
        }

        private void TrimSynchronousVirtualCache(
            SynchronousVirtualRealization realization,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            int limit = Math.Max(
                0,
                realization.Host.VirtualizationCacheItems);

            while (realization.CacheRecords != null &&
                   realization.CacheRecords.Count > limit &&
                   realization.Host.RefreshGeneration ==
                       realization.ExpectedGeneration &&
                   Object.ReferenceEquals(
                       realization.CacheRecords,
                       realization.Host.DirectVirtualCacheRecords))
            {
                RenderedItemRecord record =
                    realization.CacheRecords[0] as RenderedItemRecord;

                realization.CacheRecords.RemoveAt(0);
                DisposeSynchronousVirtualRecord(
                    realization.Host,
                    record,
                    ref cleanupErrors);
            }
        }

        /// <summary>
        /// Applies the direct viewport's current cache policy synchronously.
        /// Inactive or disposing direct hosts retain no cached control trees.
        /// A reentrant newer generation or replacement cache owns its own state
        /// and stops this pass before another record is removed.
        /// </summary>
        internal void TrimDirectVirtualizationCache(ItemsControl host)
        {
            if (host == null)
                return;

            int limit = host.DirectVirtualActive &&
                        !host.DirectVirtualDisposed &&
                        !host.IsDisposed &&
                        !host.Disposing
                ? Math.Max(0, host.VirtualizationCacheItems)
                : 0;

            TrimDirectVirtualizationCacheCore(host, limit);
        }

        /// <summary>
        /// Permanently drains every cached direct-viewport record. Disposal and
        /// deactivation use this explicit form so call ordering cannot retain a
        /// detached tree merely because the host was still marked active.
        /// </summary>
        internal void ClearDirectVirtualizationCache(ItemsControl host)
        {
            TrimDirectVirtualizationCacheCore(host, 0);
        }

        private void TrimDirectVirtualizationCacheCore(
            ItemsControl host,
            int limit)
        {
            if (host == null)
                return;

            if (host.Runtime != null &&
                !Object.ReferenceEquals(host.Runtime, this))
            {
                throw new InvalidOperationException(
                    "The ItemsControl belongs to a different XamlRuntime.");
            }

            ArrayList cache = host.DirectVirtualCacheRecords;

            if (cache == null || cache.Count == 0)
                return;

            int expectedGeneration = host.RefreshGeneration;
            SynchronousVirtualCleanupErrors cleanupErrors =
                new SynchronousVirtualCleanupErrors();

            while (cache.Count > limit &&
                   host.RefreshGeneration == expectedGeneration &&
                   Object.ReferenceEquals(
                       cache,
                       host.DirectVirtualCacheRecords))
            {
                RenderedItemRecord record =
                    cache[0] as RenderedItemRecord;

                try
                {
                    // Transfer ownership out of the shared cache before any
                    // Control or binding cleanup can invoke application code.
                    cache.RemoveAt(0);
                }
                catch (Exception ex)
                {
                    AddSynchronousVirtualCleanupError(
                        ref cleanupErrors,
                        ex);
                    break;
                }

                DisposeSynchronousVirtualRecord(
                    host,
                    record,
                    ref cleanupErrors);
            }

            if (cleanupErrors.Count > 0)
            {
                Exception[] errors =
                    new Exception[cleanupErrors.Count];
                cleanupErrors.CopyTo(errors);

                throw new SynchronousVirtualCleanupException(
                    "Direct viewport cache cleanup failed.",
                    errors[0],
                    errors);
            }
        }

        private void CleanupUnpublishedSynchronousVirtualRealization(
            SynchronousVirtualRealization realization,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            if (realization == null || realization.Published)
                return;

            int i;

            for (i = realization.Candidates.Count - 1; i >= 0; i--)
            {
                SynchronousVirtualCandidate candidate =
                    realization.Candidates[i] as
                        SynchronousVirtualCandidate;

                if (candidate == null || candidate.Record == null)
                {
                    continue;
                }

                if (candidate.ReusedCurrent)
                {
                    ItemPatchPlan patch = candidate.AppliedPatch;

                    if (patch != null && patch.Applied)
                    {
                        Exception rollbackError = RestoreItemPatchPlan(
                            patch,
                            patch.AppliedChangeCount,
                            patch.DataContextApplied,
                            realization.Host,
                            realization.ExpectedGeneration);

                        AddSynchronousVirtualCleanupError(
                            ref cleanupErrors,
                            rollbackError);
                    }

                    continue;
                }

                if (candidate.BorrowedCache &&
                    !candidate.CrossItemRecycled &&
                    realization.Host.RefreshGeneration ==
                        realization.ExpectedGeneration &&
                    Object.ReferenceEquals(
                        realization.CacheRecords,
                        realization.Host.DirectVirtualCacheRecords))
                {
                    ReturnBorrowedSynchronousVirtualRecord(
                        realization,
                        candidate,
                        ref cleanupErrors);
                }
                else
                {
                    DisposeSynchronousVirtualRecord(
                        realization.Host,
                        candidate.Record,
                        ref cleanupErrors);
                }
            }

            ResumeSynchronousVirtualLayout(
                realization,
                ref cleanupErrors);
        }

        private void ReturnBorrowedSynchronousVirtualRecord(
            SynchronousVirtualRealization realization,
            SynchronousVirtualCandidate candidate,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            try
            {
                if (candidate.Record.Control != null)
                {
                    candidate.Record.Control.Visible = false;
                    // A borrowed row may be returned to the cache after its
                    // candidate transaction is abandoned. Keep its measured
                    // size for the next realization just like a normal cache
                    // departure.

                    if (candidate.AttachedByRealization &&
                        !candidate.CacheWasParented &&
                        candidate.Record.Control.Parent != null)
                    {
                        candidate.Record.Control.Parent.Controls.Remove(
                            candidate.Record.Control);
                    }
                }

                if (candidate.BindingSlotsActivated)
                    DeactivateRenderBindingSlots(
                        candidate.Record.BindingSlots);

                int index = Math.Max(
                    0,
                    Math.Min(
                        candidate.OriginalCacheIndex,
                        realization.CacheRecords.Count));

                realization.CacheRecords.Insert(
                    index,
                    candidate.Record);
            }
            catch (Exception ex)
            {
                AddSynchronousVirtualCleanupError(
                    ref cleanupErrors,
                    ex);
                DisposeSynchronousVirtualRecord(
                    realization.Host,
                    candidate.Record,
                    ref cleanupErrors);
            }
        }

        private void DisposeSynchronousVirtualRecord(
            ItemsControl host,
            RenderedItemRecord record,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            if (record == null ||
                IsSynchronousVirtualRecordRetained(host, record))
            {
                return;
            }

            try
            {
                DisposeRenderedItemRecord(record);
            }
            catch (Exception ex)
            {
                AddSynchronousVirtualCleanupError(
                    ref cleanupErrors,
                    ex);
            }
        }

        private static bool IsSynchronousVirtualRecordRetained(
            ItemsControl host,
            RenderedItemRecord record)
        {
            if (host == null || record == null || record.Control == null)
                return false;

            if (SynchronousVirtualRecordListContainsControl(
                    host.RenderedItems,
                    record.Control))
            {
                return true;
            }

            return SynchronousVirtualRecordListContainsControl(
                host.DirectVirtualCacheRecords,
                record.Control);
        }

        private static bool SynchronousVirtualRecordListContainsControl(
            ArrayList records,
            Control control)
        {
            if (records == null || control == null)
                return false;

            int i;

            for (i = 0; i < records.Count; i++)
            {
                RenderedItemRecord current =
                    records[i] as RenderedItemRecord;

                if (current != null &&
                    Object.ReferenceEquals(
                        current.Control,
                        control))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResumeSynchronousVirtualLayout(
            SynchronousVirtualRealization realization,
            ref SynchronousVirtualCleanupErrors cleanupErrors)
        {
            if (realization == null || !realization.LayoutSuspended)
                return;

            realization.LayoutSuspended = false;

            try
            {
                realization.Host.ResumeLayout(false);
            }
            catch (Exception ex)
            {
                AddSynchronousVirtualCleanupError(
                    ref cleanupErrors,
                    ex);
            }
        }

        private static void AddSynchronousVirtualCleanupError(
            ref SynchronousVirtualCleanupErrors errors,
            Exception error)
        {
            errors.Add(error);
        }

        private static Exception CreateSynchronousVirtualFailure(
            Exception failure,
            SynchronousVirtualCleanupErrors cleanupErrors,
            bool committed)
        {
            int cleanupCount = cleanupErrors.Count;

            if (cleanupCount == 0)
                return failure;

            Exception[] errors = new Exception[cleanupCount];
            cleanupErrors.CopyTo(errors);
            Exception inner = failure == null
                ? errors[0]
                : failure;

            return new SynchronousVirtualCleanupException(
                committed
                    ? "The virtual range committed, but cleanup failed."
                    : "Virtual range realization failed and cleanup also failed.",
                inner,
                errors);
        }
    }
}
