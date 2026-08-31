using System;
using System.Collections;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime : IDisposable
    {
        private SynchronousVirtualCandidate
            TryBorrowExplicitCrossItemVirtualCacheRecord(
                SynchronousVirtualRealization realization,
                object item,
                string key,
                int index,
                int minimumCacheIndex)
        {
            ItemsControl host = realization.Host;

            if (host.ItemRecycling != ItemRecyclingMode.Explicit ||
                host.VirtualizationMode !=
                    ItemsControlVirtualizationMode.Controls)
            {
                return null;
            }

            ArrayList cache = realization.CacheRecords;
            int i;

            for (i = cache.Count - 1; i >= minimumCacheIndex; i--)
            {
                RenderedItemRecord record =
                    cache[i] as RenderedItemRecord;
                IRecyclableItemControl participant =
                    GetExplicitItemRecyclingParticipant(record);

                if (participant == null ||
                    !IsExplicitItemRecyclingRecordDetached(record))
                {
                    continue;
                }

                SynchronousVirtualCandidate candidate =
                    new SynchronousVirtualCandidate();

                candidate.Record = record;
                candidate.DesiredIndex = index;
                candidate.BorrowedCache = true;
                candidate.CrossItemRecycled = true;
                candidate.OriginalCacheIndex = i;
                candidate.CacheWasParented = false;

                // Once the application reset contract is considered, the old
                // tree can never return to the identity cache. Transfer its one
                // ownership into this staging transaction before invoking any
                // application code.
                cache.RemoveAt(i);
                realization.Candidates.Add(candidate);

                if (HasUnpatchableExplicitRecyclingSlot(record))
                {
                    RejectExplicitCrossItemCandidate(
                        realization,
                        candidate);
                    return null;
                }

                int oldIndex = record.LogicalIndex;
                object oldItem = record.Item;
                ItemRecycleContext context = new ItemRecycleContext(
                    host,
                    record.Control,
                    oldItem,
                    item,
                    oldIndex,
                    index);

                // Exceptions deliberately escape. An explicit reset callback
                // is application code; hiding its failure behind construction
                // of a fresh tree would conceal a broken recycling contract.
                bool accepted = participant.TryPrepareForRecycle(context);
                EnsureSynchronousVirtualRealizationCurrent(realization);

                if (!accepted)
                {
                    RejectExplicitCrossItemCandidate(
                        realization,
                        candidate);
                    return null;
                }

                ValidateAcceptedExplicitRecyclingRoot(record);

                // The reset contract may have changed arbitrary transient
                // properties. Force every compiled dynamic slot to restore the
                // complete XAML-owned state for the new item.
                ForceAllItemBindingSlotsToApply(record);

                ItemPatchPlan patch = CreateItemPatchPlan(
                    host,
                    record,
                    item,
                    false);

                if (patch.RequiresRebuild)
                {
                    RejectExplicitCrossItemCandidate(
                        realization,
                        candidate);
                    return null;
                }

                candidate.BindingSlotsActivated = true;

                if (!ApplyItemPatchPlan(
                        null,
                        patch,
                        host,
                        realization.ExpectedGeneration))
                {
                    throw new SynchronousVirtualSupersededException();
                }

                EnsureSynchronousVirtualRealizationCurrent(realization);

                // ApplyItemPatchPlan installs binding subscriptions. This pass
                // also assigns the new context to Function/Preset-only slots,
                // whose expressions require no observable subscription.
                ActivateRenderedItemRecordBindings(
                    record,
                    host,
                    item);
                EnsureSynchronousVirtualRealizationCurrent(realization);

                if (RenderedItemRecordHasReactiveDirtySlot(record))
                {
                    RejectExplicitCrossItemCandidate(
                        realization,
                        candidate);
                    return null;
                }

                record.Key = key;
                record.LogicalIndex = index;
                record.RealizationGeneration =
                    realization.ExpectedGeneration;
                record.VersionValue = GetDirectVirtualItemVersion(
                    host,
                    item,
                    index,
                    out record.HasVersionValue);
                record.MeasureCacheValid = false;
                record.MeasureProposedWidth = 0;
                record.MeasureProposedHeight = 0;
                record.MeasureCachedSize =
                    System.Drawing.Size.Empty;

                ApplyInheritedProperties(record.Control, host);
                EnsureSynchronousVirtualRealizationCurrent(realization);

                PerformLayoutRecursive(record.Control);
                EnsureSynchronousVirtualRealizationCurrent(realization);

                ApplyItemRootVisibility(record);
                EnsureSynchronousVirtualRealizationCurrent(realization);

                realization.Candidates.Remove(candidate);
                return candidate;
            }

            return null;
        }

        private static IRecyclableItemControl
            GetExplicitItemRecyclingParticipant(
                RenderedItemRecord record)
        {
            if (record == null || record.Control == null ||
                record.Control.IsDisposed || record.Control.Disposing)
            {
                return null;
            }

            return record.Control as IRecyclableItemControl;
        }

        private static bool IsExplicitItemRecyclingRecordDetached(
            RenderedItemRecord record)
        {
            if (record == null || record.Control == null ||
                record.Control.Parent != null ||
                RenderedItemRecordHasReactiveDirtySlot(record))
            {
                return false;
            }

            ArrayList slots = record.BindingSlots;

            if (slots == null)
                return false;

            int i;

            for (i = 0; i < slots.Count; i++)
            {
                RenderBindingSlot slot =
                    slots[i] as RenderBindingSlot;

                if (slot != null &&
                    (slot.ObservableRegistration != null ||
                     slot.Host != null ||
                     slot.DataContext != null ||
                     slot.PathResult != null ||
                     slot.ReactiveDirty))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateAcceptedExplicitRecyclingRoot(
            RenderedItemRecord record)
        {
            if (record == null || record.Control == null ||
                record.Control.IsDisposed || record.Control.Disposing ||
                record.Control.Parent != null)
            {
                throw new InvalidOperationException(
                    "IRecyclableItemControl accepted a transition after " +
                    "disposing or reparenting the detached row root.");
            }
        }

        private static void ForceAllItemBindingSlotsToApply(
            RenderedItemRecord record)
        {
            ArrayList slots = record == null
                ? null
                : record.BindingSlots;
            int i;

            for (i = slots == null ? -1 : slots.Count - 1;
                 i >= 0;
                 i--)
            {
                RenderBindingSlot slot =
                    slots[i] as RenderBindingSlot;

                if (slot != null)
                    slot.ForceNextApply = true;
            }
        }

        private static bool HasUnpatchableExplicitRecyclingSlot(
            RenderedItemRecord record)
        {
            ArrayList slots = record == null
                ? null
                : record.BindingSlots;
            int i;

            for (i = slots == null ? -1 : slots.Count - 1;
                 i >= 0;
                 i--)
            {
                RenderBindingSlot slot =
                    slots[i] as RenderBindingSlot;

                if (slot != null &&
                    (slot.Target == null ||
                     slot.Kind == RenderBindingSlotKind.RebuildOnChange))
                {
                    return true;
                }
            }

            return false;
        }

        private void RejectExplicitCrossItemCandidate(
            SynchronousVirtualRealization realization,
            SynchronousVirtualCandidate candidate)
        {
            if (candidate == null)
                return;

            // Remove transaction ownership before best-effort record disposal.
            // DisposeRenderedItemRecord detaches all owned resources before it
            // reports an error, so the rejected tree is never retried or pooled.
            realization.Candidates.Remove(candidate);
            candidate.BorrowedCache = false;

            RenderedItemRecord record = candidate.Record;
            candidate.Record = null;

            realization.Host.RecordVirtualCrossItemRecycleRejection();
            DisposeRenderedItemRecord(record);
            EnsureSynchronousVirtualRealizationCurrent(realization);
        }
    }
}
