using System;
using System.Collections;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private const int DirectVirtualMeasurementCorrectionLimit = 4;

        /// <summary>
        /// Internal control-flow signal: a realized root proved that the direct
        /// one-logical-item/one-layout-slot invariant is not true. It is handled
        /// only before publication and is never exposed as a user load failure.
        /// </summary>
        private sealed class DirectVirtualizationIneligibleException
            : Exception
        {
        }

        private struct DirectVirtualScrollAnchor
        {
            internal bool Valid;
            internal int ItemIndex;
            internal long OffsetInsideItem;
        }

        /// <summary>
        /// Builds a fresh logical-index viewport from the host's current
        /// ItemValues and realizes its initial visible range synchronously.
        /// Returns false when the normal renderer must own this source.
        /// </summary>
        internal bool ActivateDirectViewportVirtualization(
            ItemsControl host,
            bool forceRebuild,
            bool validateValues)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            if (host.IsDisposed || host.DirectVirtualDisposed)
                return false;

            VirtualItemSourceAdapter source =
                VirtualItemSourceAdapter.Create(host.ItemValues);
            int count = source.Count;

            if (!host.Virtualizing ||
                !host.AutoScroll ||
                count < host.VirtualizationThreshold ||
                !CanUseDirectViewportVirtualization(
                    host.TemplateRoot,
                    count))
            {
                return false;
            }

            // Candidate construction happens before the committed generation
            // changes. Allocation or metadata failures therefore leave the
            // previous viewport completely untouched.
            VirtualViewportModel model =
                CreateDirectVirtualViewportModel(host, count);

            VirtualItemSourceAdapter oldSource =
                host.DirectVirtualItemSource;
            ArrayList oldItemValues = host.DirectVirtualItemValues;
            VirtualViewportModel oldModel =
                host.DirectVirtualViewport;
            bool oldDirectActive = host.DirectVirtualActive;
            int oldDirectGeneration = host.DirectVirtualGeneration;
            int oldRefreshGeneration = host.RefreshGeneration;
            int oldDirectStart = host.DirectVirtualRealizedStart;
            int oldDirectEnd = host.DirectVirtualRealizedEnd;
            bool oldHasPublishedScrollAxis =
                host.DirectVirtualHasPublishedScrollAxis;
            int oldLastPublishedScrollAxis =
                host.DirectVirtualLastPublishedScrollAxis;
            int oldLastPublishedOverscanDirection =
                host.DirectVirtualLastPublishedOverscanDirection;
            Size oldAutoScrollMinSize = host.AutoScrollMinSize;
            ArrayList oldRenderedItems = host.RenderedItems;

            int expectedGeneration =
                NextDirectVirtualGeneration(oldRefreshGeneration);

            host.RefreshGeneration = expectedGeneration;
            host.DirectVirtualGeneration = expectedGeneration;
            host.DirectVirtualItemSource = source;
            host.DirectVirtualItemValues = host.ItemValues;
            host.DirectVirtualViewport = model;
            host.DirectVirtualActive = true;
            host.DirectVirtualRealizedStart = -1;
            host.DirectVirtualRealizedEnd = -1;
            host.DirectVirtualHasPublishedScrollAxis = false;
            host.DirectVirtualLastPublishedScrollAxis = 0;
            host.DirectVirtualLastPublishedOverscanDirection = 0;

            try
            {
                if (!PrepareDirectVirtualItemVersions(
                        host,
                        model,
                        expectedGeneration))
                {
                    return true;
                }

                if (!UpdateDirectVirtualScrollExtent(
                        host,
                        model,
                        expectedGeneration))
                {
                    return true;
                }

                RefreshDirectVirtualViewportSynchronouslyCore(
                    host,
                    forceRebuild,
                    false,
                    validateValues);

                return true;
            }
            catch (ItemsRefreshCommittedException)
            {
                // The manager already published the new direct range. Preserve
                // it even when later retirement or layout cleanup failed.
                throw;
            }
            catch (Exception ex)
            {
                bool ownsCandidate =
                    IsDirectVirtualGenerationCurrent(
                        host,
                        model,
                        expectedGeneration);

                if (ownsCandidate &&
                    !Object.ReferenceEquals(
                        host.RenderedItems,
                        oldRenderedItems))
                {
                    if (ex is DirectVirtualizationIneligibleException)
                    {
                        // A callback collapsed a root in the narrow window after
                        // atomic range publication but before layout. Keep that
                        // complete direct range paired with its model and let the
                        // normal renderer retire it transactionally.
                        return false;
                    }

                    throw new ItemsRefreshCommittedException(ex);
                }

                bool restored = false;

                // Restore only while the manager has not published a new
                // RenderedItems list. Post-commit cleanup/layout failures leave
                // the committed range and its model paired. A reentrant newer
                // generation must never be overwritten here either.
                if (ownsCandidate &&
                    Object.ReferenceEquals(
                        host.RenderedItems,
                        oldRenderedItems))
                {
                    host.DirectVirtualItemSource = oldSource;
                    host.DirectVirtualItemValues = oldItemValues;
                    host.DirectVirtualViewport = oldModel;
                    host.DirectVirtualActive = oldDirectActive;
                    host.DirectVirtualGeneration = oldDirectGeneration;
                    host.DirectVirtualRealizedStart = oldDirectStart;
                    host.DirectVirtualRealizedEnd = oldDirectEnd;
                    host.DirectVirtualHasPublishedScrollAxis =
                        oldHasPublishedScrollAxis;
                    host.DirectVirtualLastPublishedScrollAxis =
                        oldLastPublishedScrollAxis;
                    host.DirectVirtualLastPublishedOverscanDirection =
                        oldLastPublishedOverscanDirection;
                    // ReloadItems advances RefreshGeneration while it snapshots
                    // the requested source, before this candidate starts. When
                    // an already-active direct viewport is restored, its own
                    // committed generation is the ownership token that must be
                    // restored on both fields. Keeping the enumeration token on
                    // RefreshGeneration leaves the old viewport visible but
                    // unable to respond to scrolling; in a nested failed
                    // request it also prevents the outer candidate from owning
                    // and completing its rollback.
                    host.RefreshGeneration =
                        oldDirectActive && oldModel != null
                            ? oldDirectGeneration
                            : oldRefreshGeneration;

                    bool previousSuppress =
                        host.DirectVirtualSuppressScrollRefresh;
                    host.DirectVirtualSuppressScrollRefresh = true;

                    try
                    {
                        host.AutoScrollMinSize = oldAutoScrollMinSize;
                    }
                    catch
                    {
                        // Preserve the renderer/measurement failure that caused
                        // the rollback. The old committed records remain intact.
                    }
                    finally
                    {
                        host.DirectVirtualSuppressScrollRefresh =
                            previousSuppress;
                    }

                    restored = true;
                }

                if (restored &&
                    ex is DirectVirtualizationIneligibleException)
                {
                    // The normal keyed renderer now owns this refresh. It can
                    // represent collapsed/style-driven membership exactly.
                    return false;
                }

                throw;
            }
        }

        /// <summary>
        /// Replaces the direct viewport model from current ItemValues. This is
        /// intentionally an alias for activation so every source replacement
        /// receives one new generation and the same rollback guarantees.
        /// </summary>
        internal bool ResetDirectViewportVirtualization(
            ItemsControl host,
            bool forceRebuild,
            bool validateValues)
        {
            return ActivateDirectViewportVirtualization(
                host,
                forceRebuild,
                validateValues);
        }

        /// <summary>
        /// Reconciles, measures, and positions the visible plus overscanned
        /// range before returning to the WinForms scroll/layout event.
        /// </summary>
        internal void RefreshDirectVirtualViewportSynchronously(
            ItemsControl host,
            bool forceRebuild,
            bool validateValues)
        {
            RefreshDirectVirtualViewportSynchronouslyCore(
                host,
                forceRebuild,
                validateValues,
                false);
        }

        private void RefreshDirectVirtualViewportSynchronouslyCore(
            ItemsControl host,
            bool forceRebuild,
            bool validateValues,
            bool patchValues)
        {
            if (host == null ||
                !host.DirectVirtualActive ||
                host.DirectVirtualDisposed)
            {
                return;
            }

            VirtualViewportModel model = host.DirectVirtualViewport;
            int expectedGeneration = host.DirectVirtualGeneration;

            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                return;
            }

            ArrayList rollbackRenderedItems = host.RenderedItems;
            int rollbackRealizedStart =
                host.DirectVirtualRealizedStart;
            int rollbackRealizedEnd =
                host.DirectVirtualRealizedEnd;
            bool rollbackHasPublishedScrollAxis =
                host.DirectVirtualHasPublishedScrollAxis;
            int rollbackPublishedScrollAxis =
                host.DirectVirtualLastPublishedScrollAxis;
            int rollbackOverscanDirection =
                host.DirectVirtualLastPublishedOverscanDirection;
            Point rollbackLogicalScroll =
                GetDirectVirtualPublishedLogicalScroll(host);

            // An implicit/named style or preset can collapse a row after its
            // original direct publication. Check the committed range even when
            // its indices are unchanged; otherwise the no-reconcile fast path
            // would retain a phantom model slot indefinitely.
            RejectCollapsedPublishedDirectVirtualRoots(host);

            VirtualItemSourceAdapter source =
                host.DirectVirtualItemSource;

            if (!Object.ReferenceEquals(
                    host.DirectVirtualItemValues,
                    host.ItemValues) ||
                source == null ||
                source.Count != model.Count)
            {
                try
                {
                    ResetDirectViewportVirtualization(
                        host,
                        forceRebuild,
                        validateValues);
                }
                catch (DirectVirtualizationIneligibleException)
                {
                    throw;
                }
                catch
                {
                    RestoreDirectVirtualUnpublishedScrollOrigin(
                        host,
                        model,
                        expectedGeneration,
                        rollbackRenderedItems,
                        rollbackLogicalScroll,
                        rollbackRealizedStart,
                        rollbackRealizedEnd,
                        rollbackHasPublishedScrollAxis,
                        rollbackPublishedScrollAxis,
                        rollbackOverscanDirection);
                    throw;
                }

                return;
            }

            // Bounds/layout events raised by this same generation are already
            // covered by the active pass. A genuinely newer generation may run
            // nested; the superseded manager then observes generation loss and
            // discards only its staged work.
            if (host.DirectVirtualRefreshRunning &&
                host.DirectVirtualRefreshOwnerGeneration ==
                    expectedGeneration)
            {
                return;
            }

            bool previousRunning = host.DirectVirtualRefreshRunning;
            int previousOwner =
                host.DirectVirtualRefreshOwnerGeneration;

            host.DirectVirtualRefreshRunning = true;
            host.DirectVirtualRefreshOwnerGeneration =
                expectedGeneration;

            try
            {
                Point requestedLogicalScroll =
                    GetDirectVirtualLogicalScroll(host);
                int requestedScrollAxis =
                    host.Orientation == Orientation.Vertical
                        ? requestedLogicalScroll.Y
                        : requestedLogicalScroll.X;
                DirectVirtualScrollAnchor scrollAnchor =
                    CaptureDirectVirtualScrollAnchor(
                        model,
                        requestedScrollAxis);
                int overscanBefore;
                int overscanAfter;
                int overscanDirection =
                    GetDirectVirtualOverscanDirection(
                        host,
                        requestedScrollAxis);

                VirtualRangeCalculator.CalculateOverscanForDirection(
                    host.OverscanItems,
                    overscanDirection,
                    out overscanBefore,
                    out overscanAfter);

                VirtualItemRange range =
                    CalculateDirectVirtualRange(
                        host,
                        model,
                        overscanBefore,
                        overscanAfter);

                bool requiresReconcile =
                    forceRebuild ||
                    validateValues ||
                    patchValues ||
                    host.DirectVirtualRealizedStart !=
                        range.RealizationStartIndex ||
                    host.DirectVirtualRealizedEnd !=
                        range.RealizationEndIndex ||
                    !IsDirectVirtualPublishedRangeComplete(
                        host,
                        range);

                if (!requiresReconcile &&
                    CanPublishDirectVirtualTranslatedFrame(
                        host,
                        model,
                        expectedGeneration,
                        requestedScrollAxis))
                {
                    // SetDisplayRectLocation/AutoScrollPosition has already
                    // translated every realized child by the exact native
                    // display-origin delta. When the viewport, generation,
                    // records, and measurement caches are unchanged, running
                    // the complete measure/slot/extent pipeline would publish
                    // identical Bounds. Keep the native ScrollWindowEx move as
                    // the single visual operation for this pixel frame.
                    PublishDirectVirtualRealizedRange(
                        host,
                        range,
                        overscanDirection);
#if !WINFORMSXAML_PACKAGE
                    IncrementDirectVirtualTranslationFastPathCount(host);
#endif
                    host.RetargetActiveItemScrollAfterLayout();
                    return;
                }

                if (requiresReconcile)
                {
                    bool reconciled;

                    if (patchValues)
                    {
                        reconciled =
                            ReconcileVirtualRangeWithPatchesSynchronously(
                            host,
                            range.RealizationStartIndex,
                            range.RealizationEndIndex,
                            forceRebuild,
                            expectedGeneration);
                    }
                    else
                    {
                        reconciled = ReconcileVirtualRangeSynchronously(
                            host,
                            range.RealizationStartIndex,
                            range.RealizationEndIndex,
                            forceRebuild,
                            validateValues,
                            expectedGeneration);
                    }

                    if (!reconciled)
                        return;
                }

                if (!IsDirectVirtualGenerationCurrent(
                        host,
                        model,
                        expectedGeneration))
                {
                    return;
                }

                PublishDirectVirtualRealizedRange(
                    host,
                    range,
                    overscanDirection);

                int smallestMeasuredExtent;
                bool modelChanged = LayoutDirectVirtualRange(
                    host,
                    model,
                    expectedGeneration,
                    out smallestMeasuredExtent);
                Point positionedScroll =
                    GetDirectVirtualLogicalScroll(host);

                if (!IsDirectVirtualGenerationCurrent(
                        host,
                        model,
                        expectedGeneration))
                {
                    return;
                }

                if (!UpdateDirectVirtualScrollExtent(
                        host,
                        model,
                        expectedGeneration))
                {
                    return;
                }

                if (modelChanged)
                {
                    RestoreDirectVirtualScrollAnchor(
                        host,
                        model,
                        scrollAnchor);
                }

                bool scrollPositionChanged =
                    positionedScroll !=
                        GetDirectVirtualLogicalScroll(host);

                // A high estimate can expose only one newly measured short row
                // per correction. Batch from the smallest measured extent and
                // retain every range visited by this bounded pass. Retaining the
                // union is important: measuring a mixed-height range can move the
                // viewport into rows selected by an earlier extent map, and
                // retiring those rows before convergence exposes a blank client
                // area. Cached desired sizes keep stable rows inexpensive.
                int correctionPass = 0;

                while ((modelChanged || scrollPositionChanged) &&
                       correctionPass <
                            DirectVirtualMeasurementCorrectionLimit)
                {
                    correctionPass++;

                    VirtualItemRange measuredRange =
                        CalculateDirectVirtualRange(
                            host,
                            model,
                            overscanBefore,
                            overscanAfter);
                    int viewportAxis =
                        host.Orientation == Orientation.Vertical
                            ? host.GetItemsViewportRectangle().Height
                            : host.GetItemsViewportRectangle().Width;
                    VirtualItemRange requestedRange =
                        VirtualRangeCalculator.ExpandMeasuredRealization(
                            measuredRange,
                            model.Count,
                            viewportAxis,
                            smallestMeasuredExtent,
                            overscanBefore,
                            overscanAfter);
                    requestedRange =
                        UnionDirectVirtualRealizations(
                            range,
                            requestedRange);
                    bool realizationChanged =
                        !HaveSameDirectVirtualRealization(
                            range,
                            requestedRange);

                    if (!realizationChanged &&
                        !scrollPositionChanged)
                    {
                        return;
                    }

                    if (realizationChanged)
                    {
                        if (!ReconcileVirtualRangeSynchronously(
                                host,
                                requestedRange.RealizationStartIndex,
                                requestedRange.RealizationEndIndex,
                                false,
                                false,
                                expectedGeneration))
                        {
                            return;
                        }
                    }

                    if (!IsDirectVirtualGenerationCurrent(
                            host,
                            model,
                            expectedGeneration))
                    {
                        return;
                    }

                    PublishDirectVirtualRealizedRange(
                        host,
                        requestedRange,
                        overscanDirection);

                    modelChanged = LayoutDirectVirtualRange(
                        host,
                        model,
                        expectedGeneration,
                        out smallestMeasuredExtent);
                    positionedScroll =
                        GetDirectVirtualLogicalScroll(host);

                    if (!IsDirectVirtualGenerationCurrent(
                            host,
                            model,
                            expectedGeneration))
                    {
                        return;
                    }

                    if (!UpdateDirectVirtualScrollExtent(
                            host,
                            model,
                            expectedGeneration))
                    {
                        return;
                    }

                    if (modelChanged)
                    {
                        RestoreDirectVirtualScrollAnchor(
                            host,
                            model,
                            scrollAnchor);
                    }

                    scrollPositionChanged =
                        positionedScroll !=
                            GetDirectVirtualLogicalScroll(host);
                    range = requestedRange;
                }

                // The final measurement can move the visible indices after the
                // last bounded correction. Repair that coverage once while the
                // ranges from this pass are still retained; without this check a
                // scroll event has no guaranteed later layout to fill the hole.
                VirtualItemRange finalMeasuredRange =
                    CalculateDirectVirtualRange(
                        host,
                        model,
                        overscanBefore,
                        overscanAfter);

                if (!DoesDirectVirtualRealizationCoverVisibleRange(
                        range,
                        finalMeasuredRange))
                {
                    int viewportAxis =
                        host.Orientation == Orientation.Vertical
                            ? host.GetItemsViewportRectangle().Height
                            : host.GetItemsViewportRectangle().Width;
                    VirtualItemRange repairRange =
                        VirtualRangeCalculator.ExpandMeasuredRealization(
                            finalMeasuredRange,
                            model.Count,
                            viewportAxis,
                            // Use the smallest size observed in this measured
                            // batch. Treating every unseen row as one pixel can
                            // realize an entire small-but-complex source and
                            // defeats virtualization precisely during a fast
                            // correction. The retained union plus the final
                            // coverage pass still protects the visible range.
                            Math.Max(1, smallestMeasuredExtent),
                            overscanBefore,
                            overscanAfter);
                    repairRange =
                        UnionDirectVirtualRealizations(
                            range,
                            repairRange);

                    if (!ReconcileVirtualRangeSynchronously(
                            host,
                            repairRange.RealizationStartIndex,
                            repairRange.RealizationEndIndex,
                            false,
                            false,
                            expectedGeneration))
                    {
                        return;
                    }

                    if (!IsDirectVirtualGenerationCurrent(
                            host,
                            model,
                            expectedGeneration))
                    {
                        return;
                    }

                    PublishDirectVirtualRealizedRange(
                        host,
                        repairRange,
                        overscanDirection);
                    bool repairModelChanged =
                        LayoutDirectVirtualRange(
                        host,
                        model,
                        expectedGeneration,
                        out smallestMeasuredExtent);

                    if (!IsDirectVirtualGenerationCurrent(
                            host,
                            model,
                            expectedGeneration))
                    {
                        return;
                    }

                    positionedScroll =
                        GetDirectVirtualLogicalScroll(host);

                    if (!UpdateDirectVirtualScrollExtent(
                            host,
                            model,
                            expectedGeneration))
                    {
                        return;
                    }

                    if (repairModelChanged)
                    {
                        RestoreDirectVirtualScrollAnchor(
                            host,
                            model,
                            scrollAnchor);
                    }

                    scrollPositionChanged =
                        positionedScroll !=
                            GetDirectVirtualLogicalScroll(host);
                }

                // AutoScrollMinSize can clamp AutoScrollPosition while its
                // nested scroll/layout notification is deliberately suppressed.
                // If the bounded pass ended on that clamp, position the retained
                // controls once against the final native origin. Stable controls
                // hit the desired-size cache, so this is cheap in the common case.
                if (scrollPositionChanged)
                {
                    LayoutDirectVirtualRange(
                        host,
                        model,
                        expectedGeneration,
                        out smallestMeasuredExtent);
                }

                if (IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
                {
                    RecordDirectVirtualPublishedScrollAxis(
                        host,
                        overscanDirection);
                    RecordDirectVirtualTranslatedFrameSignature(
                        host,
                        model,
                        expectedGeneration);
                    host.RetargetActiveItemScrollAfterLayout();
                }
            }
            catch (DirectVirtualizationIneligibleException)
            {
                throw;
            }
            catch
            {
                RestoreDirectVirtualUnpublishedScrollOrigin(
                    host,
                    model,
                    expectedGeneration,
                    rollbackRenderedItems,
                    rollbackLogicalScroll,
                    rollbackRealizedStart,
                    rollbackRealizedEnd,
                    rollbackHasPublishedScrollAxis,
                    rollbackPublishedScrollAxis,
                    rollbackOverscanDirection);
                throw;
            }
            finally
            {
                if (host.DirectVirtualRefreshOwnerGeneration ==
                    expectedGeneration)
                {
                    host.DirectVirtualRefreshRunning = previousRunning;
                    host.DirectVirtualRefreshOwnerGeneration =
                        previousOwner;
                }

                host.FlushPendingThemedScrollBarSynchronization();
            }
        }

        /// <summary>
        /// Scrolls directly to an item's requested logical alignment and
        /// reconciles an immediate viewport before returning.
        /// </summary>
        internal void ScrollDirectVirtualItemIntoView(
            ItemsControl host,
            int index)
        {
            ScrollDirectVirtualItemIntoView(
                host,
                index,
                ItemScrollAlignment.Start,
                true,
                false);
        }

        internal void ScrollDirectVirtualItemIntoView(
            ItemsControl host,
            int index,
            ItemScrollAlignment alignment,
            bool hasAnimationOverride,
            bool animate)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            VirtualViewportModel model = host.DirectVirtualViewport;
            int expectedGeneration = host.DirectVirtualGeneration;

            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                return;
            }

            if (index < 0 || index >= model.Count)
                throw new ArgumentOutOfRangeException("index");

            int logicalOffset =
                CalculateDirectVirtualItemScrollTarget(
                    host,
                    model,
                    index,
                    alignment);

            if (host.ShouldAnimateItemScroll(
                    hasAnimationOverride,
                    animate))
            {
                host.ApplyItemScrollTarget(
                    logicalOffset,
                    hasAnimationOverride,
                    animate);
                return;
            }

            SetDirectVirtualItemScrollOffset(
                host,
                logicalOffset);

            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                return;
            }

            try
            {
                int correctionPass;

                for (correctionPass = 0;
                     correctionPass < 3;
                     correctionPass++)
                {
                    RefreshDirectVirtualViewportSynchronously(
                        host,
                        false,
                        false);

                    if (!IsDirectVirtualGenerationCurrent(
                            host,
                            model,
                            expectedGeneration))
                    {
                        return;
                    }

                    int correctedOffset =
                        CalculateDirectVirtualItemScrollTarget(
                            host,
                            model,
                            index,
                            alignment);

                    if (correctedOffset ==
                        host.GetLogicalScrollOffset())
                    {
                        return;
                    }

                    SetDirectVirtualItemScrollOffset(
                        host,
                        correctedOffset);
                }

                // The bounded alignment loop always publishes one viewport
                // for its final corrected offset. Stable measured ranges stop
                // after the first correction in the common case.
                RefreshDirectVirtualViewportSynchronously(
                    host,
                    false,
                    false);
            }
            catch (DirectVirtualizationIneligibleException)
            {
                // Item-aware scrolling can be the first operation that realizes a
                // style-collapsed row. Preserve the unpublished direct range and
                // transfer the source to the normal keyed renderer.
                BeginItemsRefresh(host, false);
            }
        }

        private int CalculateDirectVirtualItemScrollTarget(
            ItemsControl host,
            VirtualViewportModel model,
            int index,
            ItemScrollAlignment alignment)
        {
            return ItemsControl.CalculateItemScrollTarget(
                model.GetOffset(index),
                GetDirectVirtualItemContentExtent(
                    host,
                    model,
                    index),
                host.GetLogicalScrollOffset(),
                host.GetItemScrollViewportExtent(),
                alignment);
        }

        private static void SetDirectVirtualItemScrollOffset(
            ItemsControl host,
            int logicalOffset)
        {
            bool previousSuppress =
                host.DirectVirtualSuppressScrollRefresh;
            host.DirectVirtualSuppressScrollRefresh = true;

            try
            {
                host.SetLogicalScrollOffset(logicalOffset);
            }
            finally
            {
                host.DirectVirtualSuppressScrollRefresh =
                    previousSuppress;
            }
        }

        /// <summary>
        /// Transfers layout ownership to the normal renderer after that renderer
        /// has published its complete item list. Realized records are preserved;
        /// only detached cache hints and the direct viewport model are retired.
        /// </summary>
        internal void CommitNormalRendererFromDirectViewport(
            ItemsControl host)
        {
            if (host == null || !host.DirectVirtualActive)
                return;

            host.DirectVirtualActive = false;
            host.DirectVirtualItemSource = null;
            host.DirectVirtualItemValues = null;
            host.DirectVirtualViewport = null;
            host.DirectVirtualRealizedStart = -1;
            host.DirectVirtualRealizedEnd = -1;
            host.DirectVirtualHasPublishedScrollAxis = false;
            host.DirectVirtualLastPublishedScrollAxis = 0;
            host.DirectVirtualLastPublishedOverscanDirection = 0;
            host.DirectVirtualRefreshRunning = false;
            host.DirectVirtualRefreshOwnerGeneration = 0;
            host.DirectVirtualSuppressScrollRefresh = false;
            host.ClearDeferredDirectVirtualScrollExtent();
            host.DirectVirtualGeneration = host.RefreshGeneration;

            RestoreDirectVirtualAutoSize(host.RenderedItems);

            // Cache entries are not part of the newly committed normal list.
            // Clear them after ownership changes so reentrant layout cannot
            // re-enter the direct viewport while cleanup callbacks run.
            ClearDirectVirtualizationCache(host);
        }

        private void RestoreDirectVirtualAutoSize(ArrayList records)
        {
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                RenderedItemRecord record =
                    records[i] as RenderedItemRecord;
                Control control = record == null
                    ? null
                    : record.Control;

                if (control == null || control.IsDisposed)
                    continue;

                ElementInfo info = GetInfo(control);

                if (!info.DirectVirtualAutoSizeSuppressed)
                    continue;

                info.DirectVirtualAutoSizeSuppressed = false;
                control.AutoSize = true;
            }
        }

        /// <summary>
        /// Reconnects a preserved direct viewport after an attempted transition
        /// to the normal renderer rolled back. The normal transaction advances
        /// RefreshGeneration while it prepares its replacement tree; without
        /// this handoff the restored direct tree would remain visible but could
        /// no longer respond to scrolling or layout.
        /// </summary>
        private static void ResumeDirectViewportAfterNormalRollback(
            ItemsControl host,
            int transitionGeneration)
        {
            if (host == null ||
                host.IsDisposed ||
                host.DirectVirtualDisposed ||
                !host.DirectVirtualActive ||
                transitionGeneration < 0 ||
                host.RefreshGeneration != transitionGeneration ||
                host.DirectVirtualViewport == null ||
                host.DirectVirtualItemSource == null ||
                !Object.ReferenceEquals(
                    host.DirectVirtualItemValues,
                    host.ItemValues) ||
                host.DirectVirtualItemSource.Count !=
                    host.DirectVirtualViewport.Count)
            {
                return;
            }

            host.DirectVirtualGeneration = transitionGeneration;

            ArrayList records = host.RenderedItems;
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                RenderedItemRecord record =
                    records[i] as RenderedItemRecord;

                if (record != null)
                {
                    record.RealizationGeneration =
                        transitionGeneration;
                }
            }
        }

        /// <summary>
        /// Synchronously retires the current direct range and releases its
        /// model. A reentrant newer generation is never cleared by this call.
        /// </summary>
        internal void DeactivateDirectViewportVirtualization(
            ItemsControl host)
        {
            if (host == null)
                return;

            if (!host.DirectVirtualActive)
            {
                host.DirectVirtualItemSource = null;
                host.DirectVirtualItemValues = null;
                host.DirectVirtualViewport = null;
                host.DirectVirtualRealizedStart = -1;
                host.DirectVirtualRealizedEnd = -1;
                host.DirectVirtualHasPublishedScrollAxis = false;
                host.DirectVirtualLastPublishedScrollAxis = 0;
                host.DirectVirtualLastPublishedOverscanDirection = 0;
                host.ClearDeferredDirectVirtualScrollExtent();

                ClearDirectVirtualizationCache(host);
                return;
            }

            Exception retirementError = null;
            VirtualViewportModel model = host.DirectVirtualViewport;
            int expectedGeneration = host.DirectVirtualGeneration;

            if (model != null &&
                !IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                return;
            }

            if (IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                bool previousRunning =
                    host.DirectVirtualRefreshRunning;
                int previousOwner =
                    host.DirectVirtualRefreshOwnerGeneration;
                bool previousSuppress =
                    host.DirectVirtualSuppressScrollRefresh;

                // Empty-range retirement can detach controls and bindings,
                // which in turn raise layout/scroll callbacks. Keep those
                // same-generation callbacks inside this teardown operation so
                // they cannot realize a fresh range before active is cleared.
                host.DirectVirtualRefreshRunning = true;
                host.DirectVirtualRefreshOwnerGeneration =
                    expectedGeneration;
                host.DirectVirtualSuppressScrollRefresh = true;

                try
                {
                    ReconcileVirtualRangeSynchronously(
                        host,
                        -1,
                        -1,
                        true,
                        false,
                        expectedGeneration);
                }
                catch (Exception ex)
                {
                    retirementError = ex;
                }
                finally
                {
                    if (host.DirectVirtualRefreshOwnerGeneration ==
                        expectedGeneration)
                    {
                        host.DirectVirtualRefreshRunning =
                            previousRunning;
                        host.DirectVirtualRefreshOwnerGeneration =
                            previousOwner;
                    }

                    host.DirectVirtualSuppressScrollRefresh =
                        previousSuppress;
                }

                if (!IsDirectVirtualGenerationCurrent(
                        host,
                        model,
                        expectedGeneration))
                {
                    if (retirementError != null)
                        throw retirementError;

                    return;
                }
            }

            host.DirectVirtualActive = false;
            host.DirectVirtualItemSource = null;
            host.DirectVirtualItemValues = null;
            host.DirectVirtualViewport = null;
            host.DirectVirtualRealizedStart = -1;
            host.DirectVirtualRealizedEnd = -1;
            host.DirectVirtualHasPublishedScrollAxis = false;
            host.DirectVirtualLastPublishedScrollAxis = 0;
            host.DirectVirtualLastPublishedOverscanDirection = 0;
            host.ClearDeferredDirectVirtualScrollExtent();
            host.RefreshGeneration =
                NextDirectVirtualGeneration(host.RefreshGeneration);
            host.DirectVirtualGeneration = host.RefreshGeneration;
            int teardownGeneration = host.RefreshGeneration;

            try
            {
                ClearDirectVirtualizationCache(host);
            }
            catch (Exception ex)
            {
                retirementError = CombineDirectVirtualTeardownErrors(
                    retirementError,
                    ex);
            }

            if (!host.IsDisposed &&
                !host.DirectVirtualActive &&
                host.RefreshGeneration == teardownGeneration)
            {
                try
                {
                    host.AutoScrollMinSize = Size.Empty;
                    host.UpdateScrollExtentMarker(
                        Size.Empty,
                        Point.Empty);
                }
                catch (Exception ex)
                {
                    retirementError = CombineDirectVirtualTeardownErrors(
                        retirementError,
                        ex);
                }
            }

            if (retirementError != null)
                throw retirementError;
        }

        /// <summary>Final direct-viewport cleanup for ItemsControl disposal.</summary>
        internal void DisposeDirectViewportVirtualization(
            ItemsControl host)
        {
            if (host == null || host.DirectVirtualDisposed)
                return;

            Exception disposalError = null;

            try
            {
                DeactivateDirectViewportVirtualization(host);
            }
            catch (Exception ex)
            {
                disposalError = ex;
            }

            // Close activation before the final drain. Control/binding cleanup
            // can invoke application code; it must not be able to publish a new
            // direct generation while this host is being permanently released.
            host.DirectVirtualDisposed = true;

            try
            {
                ClearDirectVirtualizationCache(host);
            }
            catch (Exception ex)
            {
                disposalError = CombineDirectVirtualTeardownErrors(
                    disposalError,
                    ex);
            }
            finally
            {
                host.DirectVirtualRefreshRunning = false;
                host.DirectVirtualRefreshOwnerGeneration = 0;
                host.DirectVirtualSuppressScrollRefresh = false;
                host.DirectVirtualHasPublishedScrollAxis = false;
                host.DirectVirtualLastPublishedScrollAxis = 0;
                host.DirectVirtualLastPublishedOverscanDirection = 0;
                host.ClearDeferredDirectVirtualScrollExtent();
            }

            if (disposalError != null)
                throw disposalError;
        }

        private static Exception CombineDirectVirtualTeardownErrors(
            Exception primary,
            Exception cleanup)
        {
            if (primary == null)
                return cleanup;

            if (cleanup == null)
                return primary;

            Exception[] errors = new Exception[] { primary, cleanup };
            Exception combined = new SynchronousVirtualCleanupException(
                "Direct viewport teardown and cache cleanup both failed.",
                primary,
                errors);

            if (primary is ItemsRefreshCommittedException)
                return new ItemsRefreshCommittedException(combined);

            return combined;
        }

        private static VirtualViewportModel
            CreateDirectVirtualViewportModel(
                ItemsControl host,
                int count)
        {
            int spacing = host.Spacing;

            if (host.FixedItemSize > 0)
            {
                long stride = SaturatingAddNonnegative(
                    host.FixedItemSize,
                    spacing);

                return new VirtualViewportModel(count, stride);
            }

            long itemExtent = SaturatingAddNonnegative(
                host.EstimatedItemSize,
                spacing);

            return new VirtualViewportModel(
                count,
                itemExtent,
                host.EstimatedItemSize);
        }

        private bool PrepareDirectVirtualItemVersions(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration)
        {
            if (String.IsNullOrEmpty(host.ItemVersionPath))
                return true;

            model.InitializeItemVersionSnapshot();

            return IsDirectVirtualGenerationCurrent(
                host,
                model,
                expectedGeneration);
        }

        private object GetDirectVirtualItemVersion(
            ItemsControl host,
            object item,
            int index,
            out bool hasValue)
        {
            VirtualViewportModel model = host == null
                ? null
                : host.DirectVirtualViewport;
            object value;

            if (model != null &&
                index >= 0 &&
                index < model.Count &&
                model.TryGetItemVersion(
                    index,
                    out value,
                    out hasValue))
            {
                return value;
            }

            value = GetItemVersionValue(host, item, out hasValue);

            if (model != null &&
                host != null &&
                !String.IsNullOrEmpty(host.ItemVersionPath) &&
                index >= 0 &&
                index < model.Count &&
                Object.ReferenceEquals(
                    host.DirectVirtualViewport,
                    model))
            {
                model.SetItemVersion(index, value, hasValue);
            }

            return value;
        }

        private VirtualItemRange CalculateDirectVirtualRange(
            ItemsControl host,
            VirtualViewportModel model,
            int overscanBefore,
            int overscanAfter)
        {
            Rectangle viewport = host.GetItemsViewportRectangle();
            Point logicalScroll = GetDirectVirtualLogicalScroll(host);

            if (model.Uniform)
            {
                return VirtualRangeCalculator.Calculate(
                    model.Count,
                    host.FixedItemSize,
                    host.Spacing,
                    host.Orientation,
                    host.Padding,
                    viewport.Size,
                    logicalScroll,
                    overscanBefore,
                    overscanAfter);
            }

            return CalculateGeneralDirectVirtualRange(
                host,
                model,
                viewport.Size,
                logicalScroll,
                overscanBefore,
                overscanAfter);
        }

        private static VirtualItemRange
            CalculateGeneralDirectVirtualRange(
                ItemsControl host,
                VirtualViewportModel model,
                Size viewportSize,
                Point logicalScroll,
                int overscanBefore,
                int overscanAfter)
        {
            if (model.Count == 0)
                return VirtualItemRange.Empty;

            int viewport =
                host.Orientation == Orientation.Vertical
                    ? viewportSize.Height
                    : viewportSize.Width;
            int scroll =
                host.Orientation == Orientation.Vertical
                    ? logicalScroll.Y
                    : logicalScroll.X;

            if (viewport <= 0)
                return VirtualItemRange.Empty;

            // viewportSize is already the host's inner rectangle. The native
            // scroll value is consequently in the same logical item coordinate
            // system; applying leading padding again shifts every range.
            long viewportStart = scroll;
            long contentEnd = GetDirectVirtualContentExtent(
                host,
                model);

            if (contentEnd <= 0 || viewportStart >= contentEnd)
                return VirtualItemRange.Empty;

            long viewportEnd = Math.Min(
                contentEnd,
                viewportStart + (long)viewport);

            if (viewportEnd <= viewportStart)
                return VirtualItemRange.Empty;

            int first = model.FindIndexAtOffset(viewportStart);

            if (first < 0)
                return VirtualItemRange.Empty;

            long firstStart = model.GetOffset(first);
            long firstContentEnd = SaturatingAddNonnegative(
                firstStart,
                GetDirectVirtualItemContentExtent(
                    host,
                    model,
                    first));

            // The extent model includes trailing spacing. A viewport wholly in
            // that half-open gap must advance rather than realize the row that
            // has already ended.
            if (viewportStart >= firstContentEnd)
                first++;

            if (first >= model.Count ||
                model.GetOffset(first) >= viewportEnd)
            {
                return VirtualItemRange.Empty;
            }

            int last = model.FindIndexAtOffset(viewportEnd - 1L);

            if (last < first)
                return VirtualItemRange.Empty;

            long realizationStart =
                (long)first - (long)overscanBefore;
            long realizationEnd =
                (long)last + (long)overscanAfter;

            if (realizationStart < 0)
                realizationStart = 0;
            if (realizationEnd >= model.Count)
                realizationEnd = model.Count - 1L;

            return new VirtualItemRange(
                first,
                last,
                (int)realizationStart,
                (int)realizationEnd);
        }

        private bool LayoutDirectVirtualRange(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration,
            out int smallestPositiveMeasuredExtent)
        {
            smallestPositiveMeasuredExtent = 0;

            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                return false;
            }

            Rectangle viewport = host.GetItemsViewportRectangle();
            int availableWidth = Math.Max(0, viewport.Width);
            int availableHeight = Math.Max(0, viewport.Height);
            ArrayList records = host.RenderedItems;
            bool modelChanged = false;
            int i;

            if (records == null)
                return false;

            // Measure only the manager's realized range. Point updates are
            // published immediately after each measurement has returned and
            // the captured generation has been revalidated.
            for (i = 0; i < records.Count; i++)
            {
                if (!IsDirectVirtualGenerationCurrent(
                        host,
                        model,
                        expectedGeneration) ||
                    !Object.ReferenceEquals(
                        records,
                        host.RenderedItems))
                {
                    return modelChanged;
                }

                RenderedItemRecord record =
                    records[i] as RenderedItemRecord;

                if (record == null ||
                    record.LogicalIndex < 0 ||
                    record.LogicalIndex >= model.Count ||
                    record.Control == null ||
                    record.Control.IsDisposed)
                {
                    continue;
                }

                Control child = record.Control;
                ElementInfo info = GetInfo(child);

                if (info.Collapsed)
                    throw new DirectVirtualizationIneligibleException();

                if (host.FixedItemSize > 0 && child.AutoSize)
                {
                    // FixedItemSize owns the scrolling-axis geometry. Native
                    // AutoSize controls otherwise shrink after SetBounds and
                    // leave a visible hole between adjacent logical slots.
                    info.DirectVirtualAutoSizeSuppressed = true;
                    child.AutoSize = false;
                }

                Padding margin =
                    GetEffectiveMargin(child, info.Margin);
                Size proposed = new Size(
                    SubtractDimensions(
                        availableWidth,
                        margin.Left,
                        margin.Right),
                    SubtractDimensions(
                        availableHeight,
                        margin.Top,
                        margin.Bottom));
                Size desired = GetCachedDesiredSize(
                    host,
                    record,
                    child,
                    proposed,
                    false);

                if (!IsDirectVirtualGenerationCurrent(
                        host,
                        model,
                        expectedGeneration) ||
                    !Object.ReferenceEquals(
                        records,
                        host.RenderedItems))
                {
                    return modelChanged;
                }

                long measuredContent =
                    host.Orientation == Orientation.Vertical
                        ? SaturatingAddNonnegative(
                            desired.Height,
                            SaturatingAddNonnegative(
                                margin.Top,
                                margin.Bottom))
                        : SaturatingAddNonnegative(
                            desired.Width,
                            SaturatingAddNonnegative(
                                margin.Left,
                                margin.Right));

                if (measuredContent < 1)
                    measuredContent = 1;

                if (host.FixedItemSize > 0)
                    measuredContent = host.FixedItemSize;

                int positiveMeasuredExtent =
                    ClampPositiveLongToInt(measuredContent);

                if (smallestPositiveMeasuredExtent == 0 ||
                    positiveMeasuredExtent <
                        smallestPositiveMeasuredExtent)
                {
                    smallestPositiveMeasuredExtent =
                        positiveMeasuredExtent;
                }

                if (!model.Uniform)
                {
                    long measuredExtent = measuredContent;

                    if (record.LogicalIndex + 1 < model.Count)
                    {
                        measuredExtent = SaturatingAddNonnegative(
                            measuredExtent,
                            host.Spacing);
                    }

                    if (model.GetExtent(record.LogicalIndex) !=
                        measuredExtent)
                    {
                        model.SetExtent(
                            record.LogicalIndex,
                            measuredExtent);
                        modelChanged = true;
                    }
                }
            }

            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration) ||
                !Object.ReferenceEquals(
                    records,
                    host.RenderedItems))
            {
                return modelChanged;
            }

            Point scroll = host.AutoScrollPosition;
            Point logicalScroll =
                GetDirectVirtualLogicalScroll(host);
            int originX = host.AutoScroll ? scroll.X : 0;
            int originY = host.AutoScroll ? scroll.Y : 0;
            long contentLeft =
                (long)originX + (long)viewport.X;
            long contentTop =
                (long)originY + (long)host.Padding.Top;
            bool rtl = host.ContentRightToLeft;
            bool previousPositioning =
                host.DirectVirtualPositioningControls;
            host.DirectVirtualPositioningControls = true;

            try
            {
                for (i = 0; i < records.Count; i++)
                {
                    if (!IsDirectVirtualGenerationCurrent(
                            host,
                            model,
                            expectedGeneration) ||
                        !Object.ReferenceEquals(
                            records,
                            host.RenderedItems))
                    {
                        return modelChanged;
                    }

                    RenderedItemRecord record =
                        records[i] as RenderedItemRecord;

                    if (record == null ||
                        record.LogicalIndex < 0 ||
                        record.LogicalIndex >= model.Count ||
                        record.Control == null ||
                        record.Control.IsDisposed)
                    {
                        continue;
                    }

                    Control child = record.Control;

                    // A binding/style callback can collapse the root after the
                    // measurement loop. Revalidate immediately before publishing
                    // bounds so an unchanged already-complete range cannot retain a
                    // phantom fixed/estimated slot.
                    if (GetInfo(child).Collapsed)
                        throw new DirectVirtualizationIneligibleException();

                    int contentExtent = ClampPositiveLongToInt(
                        GetDirectVirtualItemContentExtent(
                            host,
                            model,
                            record.LogicalIndex));
                    long logical = model.GetOffset(record.LogicalIndex);
                    Rectangle slot;

                    if (host.Orientation == Orientation.Vertical)
                    {
                        slot = new Rectangle(
                            ClampLongToInt(contentLeft),
                            ClampLongToInt(
                                SaturatingAddSigned(
                                    contentTop,
                                    logical)),
                            availableWidth,
                            contentExtent);
                    }
                    else
                    {
                        long x;

                        if (rtl)
                        {
                            // Native AutoScroll always moves child windows toward
                            // negative X. RTL content grows toward negative logical
                            // X, so its logical scroll must be added back here. The
                            // previous -scroll-logical formula moved the selected
                            // row twice as far left on every scroll event.
                            x = SaturatingAddSigned(
                                host.Padding.Left,
                                availableWidth);
                            x = SaturatingAddSigned(
                                x,
                                logicalScroll.X);
                            x = SaturatingAddSigned(x, -logical);
                            x = SaturatingAddSigned(
                                x,
                                -(long)contentExtent);
                        }
                        else
                        {
                            x = SaturatingAddSigned(
                                contentLeft,
                                logical);
                        }

                        slot = new Rectangle(
                            ClampLongToInt(x),
                            ClampLongToInt(contentTop),
                            contentExtent,
                            availableHeight);
                    }

                    LayoutControlInSlotWithDesired(
                        child,
                        slot,
                        host.Orientation == Orientation.Vertical ||
                            host.FixedItemSize > 0,
                        host.Orientation == Orientation.Horizontal ||
                            host.FixedItemSize > 0,
                        record.MeasureCachedSize);

                    if (!IsDirectVirtualGenerationCurrent(
                            host,
                            model,
                            expectedGeneration) ||
                        !Object.ReferenceEquals(
                            records,
                            host.RenderedItems))
                    {
                        return modelChanged;
                    }
                }
            }
            finally
            {
                host.DirectVirtualPositioningControls =
                    previousPositioning;
            }

            return modelChanged;
        }

        private bool UpdateDirectVirtualScrollExtent(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration)
        {
            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                return false;
            }

            long contentExtent = GetDirectVirtualContentExtent(
                host,
                model);
            Size nativeExtent;

            if (model.Count == 0)
            {
                nativeExtent = Size.Empty;
            }
            else if (host.Orientation == Orientation.Vertical)
            {
                long height = SaturatingAddNonnegative(
                    contentExtent,
                    SaturatingAddNonnegative(
                        host.Padding.Top,
                        host.Padding.Bottom));

                nativeExtent = new Size(
                    1,
                    Math.Max(1, ClampNonnegativeLongToInt(height)));
            }
            else
            {
                long width = SaturatingAddNonnegative(
                    contentExtent,
                    SaturatingAddNonnegative(
                        host.Padding.Left,
                        host.Padding.Right));

                nativeExtent = new Size(
                    Math.Max(1, ClampNonnegativeLongToInt(width)),
                    1);
            }

            Size markerExtent =
                host.Orientation == Orientation.Vertical
                    ? new Size(
                        1,
                        Math.Max(
                            1,
                            ClampNonnegativeLongToInt(contentExtent)))
                    : new Size(
                        Math.Max(
                            1,
                            ClampNonnegativeLongToInt(contentExtent)),
                        1);

            if (host.ShouldDeferDirectVirtualScrollExtent)
            {
                // Measured item sizes may refine the model throughout a wheel
                // burst, but changing AutoScrollMinSize here lets native
                // ScrollableControl add/remove chrome and resize ClientSize in
                // the middle of the styled gesture. Stage only the latest
                // extent; the settled frame publishes it once.
                host.DirectVirtualScrollExtentDeferred = true;
                host.DirectVirtualDeferredNativeExtent = nativeExtent;
                host.DirectVirtualDeferredMarkerExtent = markerExtent;
                host.DirectVirtualDeferredContentExtent = contentExtent;
                host.DirectVirtualDeferredExtentGeneration =
                    expectedGeneration;
                return true;
            }

            return PublishDirectVirtualScrollExtent(
                host,
                model,
                expectedGeneration,
                nativeExtent,
                markerExtent,
                contentExtent);
        }

        internal void FlushDeferredDirectVirtualScrollExtent(
            ItemsControl host)
        {
            if (host == null ||
                !host.DirectVirtualScrollExtentDeferred)
            {
                return;
            }

            Size nativeExtent = host.DirectVirtualDeferredNativeExtent;
            Size markerExtent = host.DirectVirtualDeferredMarkerExtent;
            long contentExtent =
                host.DirectVirtualDeferredContentExtent;
            int expectedGeneration =
                host.DirectVirtualDeferredExtentGeneration;
            VirtualViewportModel model = host.DirectVirtualViewport;

            host.ClearDeferredDirectVirtualScrollExtent();

            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration))
            {
                return;
            }

            PublishDirectVirtualScrollExtent(
                host,
                model,
                expectedGeneration,
                nativeExtent,
                markerExtent,
                contentExtent);
            host.FlushPendingThemedScrollBarSynchronization();
        }

        private bool PublishDirectVirtualScrollExtent(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration,
            Size nativeExtent,
            Size markerExtent,
            long contentExtent)
        {

            bool previousSuppress =
                host.DirectVirtualSuppressScrollRefresh;
            host.DirectVirtualSuppressScrollRefresh = true;

            try
            {
                if (host.AutoScrollMinSize != nativeExtent)
                    host.AutoScrollMinSize = nativeExtent;

                ClampDirectVirtualScrollOrigin(
                    host,
                    contentExtent);

                if (!IsDirectVirtualGenerationCurrent(
                        host,
                        model,
                        expectedGeneration))
                {
                    return false;
                }

                host.UpdateScrollExtentMarker(
                    markerExtent,
                    host.AutoScrollPosition);
            }
            finally
            {
                host.DirectVirtualSuppressScrollRefresh =
                    previousSuppress;
            }

            return IsDirectVirtualGenerationCurrent(
                host,
                model,
                expectedGeneration);
        }

        private void ClampDirectVirtualScrollOrigin(
            ItemsControl host,
            long contentExtent)
        {
            Rectangle viewport = host.GetItemsViewportRectangle();
            int viewportAxis = host.Orientation == Orientation.Vertical
                ? viewport.Height
                : viewport.Width;
            long maximumOrigin = Math.Max(
                0L,
                contentExtent - Math.Max(0, viewportAxis));
            Point logical = GetDirectVirtualLogicalScroll(host);
            int current = host.Orientation == Orientation.Vertical
                ? logical.Y
                : logical.X;
            int target = (long)current <= maximumOrigin
                ? current
                : ClampNonnegativeLongToInt(maximumOrigin);

            // Set even when L remains unchanged. Horizontal RTL publishes
            // P=M-L, so an extent/range change can require a new physical
            // origin without changing the framework logical viewport.
            host.SetLogicalScrollOffset(target);
        }

        private static long GetDirectVirtualContentExtent(
            ItemsControl host,
            VirtualViewportModel model)
        {
            long contentExtent = model.TotalExtent;

            // Uniform mode keeps a constant stride for O(1) index/offset math.
            // Its final synthetic gap is excluded from the native scroll range.
            if (model.Uniform &&
                model.Count > 0 &&
                host.Spacing > 0)
            {
                contentExtent -= host.Spacing;
            }

            return Math.Max(0L, contentExtent);
        }

        private static DirectVirtualScrollAnchor
            CaptureDirectVirtualScrollAnchor(
                VirtualViewportModel model,
                int logicalOffset)
        {
            DirectVirtualScrollAnchor anchor =
                new DirectVirtualScrollAnchor();

            if (model == null ||
                model.Count == 0 ||
                model.TotalExtent <= 0L)
            {
                return anchor;
            }

            long normalized = Math.Max(0L, (long)logicalOffset);

            if (normalized >= model.TotalExtent)
                normalized = model.TotalExtent - 1L;

            int index = model.FindIndexAtOffset(normalized);

            if (index < 0)
                return anchor;

            anchor.Valid = true;
            anchor.ItemIndex = index;
            anchor.OffsetInsideItem = Math.Max(
                0L,
                normalized - model.GetOffset(index));
            return anchor;
        }

        private static void RestoreDirectVirtualScrollAnchor(
            ItemsControl host,
            VirtualViewportModel model,
            DirectVirtualScrollAnchor anchor)
        {
            if (!anchor.Valid ||
                host == null ||
                model == null ||
                anchor.ItemIndex < 0 ||
                anchor.ItemIndex >= model.Count)
            {
                return;
            }

            long extent = model.GetExtent(anchor.ItemIndex);
            long inside = extent <= 0L
                ? 0L
                : Math.Min(
                    anchor.OffsetInsideItem,
                    extent - 1L);
            long requested = model.GetOffset(anchor.ItemIndex) + inside;
            host.SetLogicalScrollOffset(
                ClampNonnegativeLongToInt(requested));
        }

        private static long GetDirectVirtualItemContentExtent(
            ItemsControl host,
            VirtualViewportModel model,
            int index)
        {
            long extent = model.GetExtent(index);

            if (host.Spacing > 0 &&
                (model.Uniform || index + 1 < model.Count))
            {
                extent = Math.Max(0L, extent - host.Spacing);
            }

            return extent;
        }

        private static bool IsDirectVirtualGenerationCurrent(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration)
        {
            return host != null &&
                   !host.IsDisposed &&
                   !host.DirectVirtualDisposed &&
                   host.DirectVirtualActive &&
                   Object.ReferenceEquals(
                       host.DirectVirtualViewport,
                       model) &&
                   host.DirectVirtualGeneration == expectedGeneration &&
                   host.RefreshGeneration == expectedGeneration;
        }

        private static void PublishDirectVirtualRealizedRange(
            ItemsControl host,
            VirtualItemRange range,
            int overscanDirection)
        {
            host.DirectVirtualRealizedStart =
                range.RealizationStartIndex;
            host.DirectVirtualRealizedEnd =
                range.RealizationEndIndex;

            RecordDirectVirtualPublishedScrollAxis(
                host,
                overscanDirection);
        }

        private static void RecordDirectVirtualPublishedScrollAxis(
            ItemsControl host)
        {
            Point logicalScroll = GetDirectVirtualLogicalScroll(host);
            int scrollAxis = host.Orientation == Orientation.Vertical
                ? logicalScroll.Y
                : logicalScroll.X;

            RecordDirectVirtualPublishedScrollAxis(
                host,
                GetDirectVirtualOverscanDirection(host, scrollAxis));
        }

        private static Point GetDirectVirtualPublishedLogicalScroll(
            ItemsControl host)
        {
            Point logicalScroll = GetDirectVirtualLogicalScroll(host);

            if (!host.DirectVirtualHasPublishedScrollAxis)
                return logicalScroll;

            return host.Orientation == Orientation.Vertical
                ? new Point(
                    logicalScroll.X,
                    host.DirectVirtualLastPublishedScrollAxis)
                : new Point(
                    host.DirectVirtualLastPublishedScrollAxis,
                    logicalScroll.Y);
        }

        private static void RestoreDirectVirtualUnpublishedScrollOrigin(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration,
            ArrayList rollbackRenderedItems,
            Point rollbackLogicalScroll,
            int rollbackRealizedStart,
            int rollbackRealizedEnd,
            bool rollbackHasPublishedScrollAxis,
            int rollbackPublishedScrollAxis,
            int rollbackOverscanDirection)
        {
            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration) ||
                !Object.ReferenceEquals(
                    rollbackRenderedItems,
                    host.RenderedItems))
            {
                return;
            }

            try
            {
                RestoreItemsScrollPosition(
                    host,
                    rollbackLogicalScroll.X,
                    rollbackLogicalScroll.Y);
            }
            catch
            {
                // Preserve the realization error. The committed range remains
                // authoritative even if the native host rejects restoration.
            }

            if (!IsDirectVirtualGenerationCurrent(
                    host,
                    model,
                    expectedGeneration) ||
                !Object.ReferenceEquals(
                    rollbackRenderedItems,
                    host.RenderedItems))
            {
                return;
            }

            host.DirectVirtualRealizedStart = rollbackRealizedStart;
            host.DirectVirtualRealizedEnd = rollbackRealizedEnd;
            host.DirectVirtualHasPublishedScrollAxis =
                rollbackHasPublishedScrollAxis;
            host.DirectVirtualLastPublishedScrollAxis =
                rollbackPublishedScrollAxis;
            host.DirectVirtualLastPublishedOverscanDirection =
                rollbackOverscanDirection;
            host.Invalidate(false);
        }

        private static void RecordDirectVirtualPublishedScrollAxis(
            ItemsControl host,
            int overscanDirection)
        {
            Point logicalScroll = GetDirectVirtualLogicalScroll(host);
            host.DirectVirtualLastPublishedScrollAxis =
                host.Orientation == Orientation.Vertical
                    ? logicalScroll.Y
                    : logicalScroll.X;
            host.DirectVirtualHasPublishedScrollAxis = true;
            host.DirectVirtualLastPublishedOverscanDirection =
                overscanDirection;
        }

        private static int GetDirectVirtualOverscanDirection(
            ItemsControl host,
            int requestedScrollAxis)
        {
            if (!host.DirectVirtualHasPublishedScrollAxis)
                return 0;

            if (requestedScrollAxis >
                host.DirectVirtualLastPublishedScrollAxis)
            {
                return 1;
            }

            if (requestedScrollAxis <
                host.DirectVirtualLastPublishedScrollAxis)
            {
                return -1;
            }

            // Native WinForms can report the same settled offset again from a
            // nested layout/scroll callback. Retaining the direction used by
            // the committed range makes that duplicate request an identity
            // operation instead of replacing rows solely to rebalance cache.
            return host.DirectVirtualLastPublishedOverscanDirection;
        }

        private static bool HaveSameDirectVirtualRealization(
            VirtualItemRange left,
            VirtualItemRange right)
        {
            return left.RealizationStartIndex ==
                       right.RealizationStartIndex &&
                   left.RealizationEndIndex ==
                       right.RealizationEndIndex;
        }

        private bool CanPublishDirectVirtualTranslatedFrame(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration,
            int requestedScrollAxis)
        {
            if (host == null ||
                !host.DirectVirtualHasPublishedScrollAxis ||
                requestedScrollAxis ==
                    host.DirectVirtualLastPublishedScrollAxis ||
                !Object.ReferenceEquals(
                    host.DirectVirtualTranslatedFrameModel,
                    model) ||
                !Object.ReferenceEquals(
                    host.DirectVirtualTranslatedFrameRecords,
                    host.RenderedItems) ||
                host.DirectVirtualTranslatedFrameGeneration !=
                    expectedGeneration ||
                host.DirectVirtualTranslatedFrameViewport !=
                    host.GetItemsViewportRectangle())
            {
                return false;
            }

            ArrayList records = host.RenderedItems;

            if (records == null)
                return false;

            int i;

            for (i = 0; i < records.Count; i++)
            {
                RenderedItemRecord record =
                    records[i] as RenderedItemRecord;

                // Layout-affecting reactive changes invalidate this bit. The
                // next frame then performs the normal measurement and records
                // a new safe translation signature.
                if (record == null ||
                    !record.MeasureCacheValid ||
                    record.Control == null ||
                    record.Control.IsDisposed)
                {
                    return false;
                }
            }

            return true;
        }

        private static void RecordDirectVirtualTranslatedFrameSignature(
            ItemsControl host,
            VirtualViewportModel model,
            int expectedGeneration)
        {
            host.DirectVirtualTranslatedFrameModel = model;
            host.DirectVirtualTranslatedFrameRecords = host.RenderedItems;
            host.DirectVirtualTranslatedFrameViewport =
                host.GetItemsViewportRectangle();
            host.DirectVirtualTranslatedFrameGeneration =
                expectedGeneration;
        }

#if !WINFORMSXAML_PACKAGE
        private static void
            IncrementDirectVirtualTranslationFastPathCount(
                ItemsControl host)
        {
            if (host.DirectVirtualTranslationFastPathCount < Int64.MaxValue)
                host.DirectVirtualTranslationFastPathCount++;
        }
#endif

        private static bool DoesDirectVirtualRealizationCoverVisibleRange(
            VirtualItemRange realized,
            VirtualItemRange visible)
        {
            if (visible.IsEmpty)
                return true;
            if (realized.IsEmpty)
                return false;

            return realized.RealizationStartIndex <=
                       visible.FirstVisibleIndex &&
                   realized.RealizationEndIndex >=
                       visible.LastVisibleIndex;
        }

        private static VirtualItemRange UnionDirectVirtualRealizations(
            VirtualItemRange current,
            VirtualItemRange requested)
        {
            if (current.IsEmpty)
                return requested;
            if (requested.IsEmpty)
                return current;

            return new VirtualItemRange(
                requested.FirstVisibleIndex,
                requested.LastVisibleIndex,
                Math.Min(
                    current.RealizationStartIndex,
                    requested.RealizationStartIndex),
                Math.Max(
                    current.RealizationEndIndex,
                    requested.RealizationEndIndex));
        }

        private static bool IsDirectVirtualPublishedRangeComplete(
            ItemsControl host,
            VirtualItemRange range)
        {
            int expectedCount = range.IsEmpty
                ? 0
                : range.RealizationEndIndex -
                    range.RealizationStartIndex + 1;

            if (host.RenderedItems == null ||
                host.RenderedItems.Count != expectedCount)
            {
                return false;
            }

            int i;

            for (i = 0; i < expectedCount; i++)
            {
                RenderedItemRecord record =
                    host.RenderedItems[i] as RenderedItemRecord;

                if (record == null ||
                    record.Control == null ||
                    record.Control.IsDisposed ||
                    record.LogicalIndex !=
                        range.RealizationStartIndex + i)
                {
                    return false;
                }
            }

            return true;
        }

        private static Point GetDirectVirtualLogicalScroll(
            ItemsControl host)
        {
            int logical = host.GetLogicalScrollOffset();

            return host.Orientation == Orientation.Vertical
                ? new Point(0, logical)
                : new Point(logical, 0);
        }

        private static int NextDirectVirtualGeneration(int current)
        {
            return current == Int32.MaxValue
                ? 1
                : current + 1;
        }

        private static int SubtractDimensions(
            int dimension,
            int leading,
            int trailing)
        {
            long result = (long)dimension -
                (long)leading - (long)trailing;

            return result <= 0
                ? 0
                : ClampNonnegativeLongToInt(result);
        }

        private static long SaturatingAddNonnegative(
            long left,
            long right)
        {
            if (left < 0)
                left = 0;
            if (right < 0)
                right = 0;

            return right > Int64.MaxValue - left
                ? Int64.MaxValue
                : left + right;
        }

        private static long SaturatingAddSigned(
            long left,
            long right)
        {
            if (right > 0 && left > Int64.MaxValue - right)
                return Int64.MaxValue;

            if (right < 0 && left < Int64.MinValue - right)
                return Int64.MinValue;

            return left + right;
        }

        private static int ClampPositiveLongToInt(long value)
        {
            if (value <= 1)
                return 1;

            return value >= Int32.MaxValue
                ? Int32.MaxValue
                : (int)value;
        }

        private static int ClampNonnegativeLongToInt(long value)
        {
            if (value <= 0)
                return 0;

            return value >= Int32.MaxValue
                ? Int32.MaxValue
                : (int)value;
        }

        private static int ClampLongToInt(long value)
        {
            if (value <= Int32.MinValue)
                return Int32.MinValue;
            if (value >= Int32.MaxValue)
                return Int32.MaxValue;

            return (int)value;
        }
    }
}
