using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Xml;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        private bool TryBeginLightweightItemsRefresh(
            ItemsControl host,
            bool forceRebuild)
        {
            if (host.VirtualizationMode !=
                ItemsControlVirtualizationMode.Lightweight)
            {
                if (host.LightweightActive)
                    DeactivateLightweightItemsControl(host);

                return false;
            }

            host.SetRefreshing(true, null);
            bool wasLightweightActive = host.LightweightActive;
            Size rollbackAutoScrollMinSize = host.AutoScrollMinSize;
            Point rollbackScroll = host.AutoScrollPosition;
            int transitionGeneration = host.RefreshGeneration;
            bool publicationStarted = false;

            try
            {
                EnsureLightweightHostIsEligible(host);

                if (host.LightweightPlan == null ||
                    !String.Equals(
                        host.LightweightPlan.TemplateXml,
                        host.TemplateOuterXml,
                        StringComparison.Ordinal))
                {
                    ValidateLightweightItemsControlConfiguration(host);
                }

                // Finish the potentially throwing style/extent activation while
                // the currently committed Control rows are still intact. Only
                // after this succeeds do we cross the publication boundary and
                // retire native row trees.
                ActivateLightweightItemsControl(host);

                if (!PrepareLightweightVisibleRows(
                        host,
                        transitionGeneration) ||
                    !OwnsItemsTransition(host, transitionGeneration))
                {
                    return true;
                }

                publicationStarted = true;
                Exception retirementError = null;

                if (host.DirectVirtualActive)
                {
                    try
                    {
                        DeactivateDirectViewportVirtualization(host);
                    }
                    catch (Exception ex)
                    {
                        retirementError = FirstItemsCommitError(
                            retirementError,
                            ex);
                    }
                }
                else
                {
                    retirementError =
                        RetireControlRowsForLightweight(host);
                }

                if (!OwnsItemsTransition(host, transitionGeneration))
                    return true;

                Exception completionError =
                    CompleteLightweightItemsRefresh(
                        host,
                        retirementError);

                if (completionError != null)
                    throw new ItemsRefreshCommittedException(completionError);

                return true;
            }
            catch (ItemsRefreshCommittedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (!OwnsItemsTransition(host, transitionGeneration))
                    return true;

                if (publicationStarted)
                {
                    Exception completionError =
                        CompleteLightweightItemsRefresh(host, ex);
                    throw new ItemsRefreshCommittedException(
                        completionError == null ? ex : completionError);
                }

                Exception failure = ex;

                if (!wasLightweightActive && host.LightweightActive)
                {
                    try
                    {
                        RollbackLightweightActivation(
                            host,
                            rollbackAutoScrollMinSize,
                            rollbackScroll);
                    }
                    catch (Exception rollbackError)
                    {
                        failure = IncludeItemsRollbackError(
                            failure,
                            rollbackError);
                    }
                }
                else if (wasLightweightActive && host.LightweightActive)
                {
                    try
                    {
                        RestoreLightweightScrollState(
                            host,
                            rollbackAutoScrollMinSize,
                            rollbackScroll);
                    }
                    catch (Exception rollbackError)
                    {
                        failure = IncludeItemsRollbackError(
                            failure,
                            rollbackError);
                    }
                }

                RestoreCommittedItemsSource(host);
                host.SetRefreshing(false, failure);

                try
                {
                    host.RaiseRefreshFailed();
                }
                catch (Exception callbackError)
                {
                    failure = FirstItemsCommitError(
                        failure,
                        callbackError);
                    host.SetRefreshing(false, failure);
                }

                throw new ItemsRefreshFailedException(failure);
            }
        }

        private Exception CompleteLightweightItemsRefresh(
            ItemsControl host,
            Exception currentError)
        {
            try
            {
                CommitItemsSource(host);
            }
            catch (Exception ex)
            {
                currentError = FirstItemsCommitError(
                    currentError,
                    ex);
            }

            host.SetRefreshing(false, null);

            try
            {
                RefreshItemsControlPresetIndex(host, true);
            }
            catch (Exception ex)
            {
                currentError = FirstItemsCommitError(
                    currentError,
                    ex);
            }

            try
            {
                host.RaiseRefreshCompleted();
            }
            catch (Exception ex)
            {
                currentError = FirstItemsCommitError(
                    currentError,
                    ex);
            }

            return currentError;
        }

        private void RollbackLightweightActivation(
            ItemsControl host,
            Size autoScrollMinSize,
            Point scrollPosition)
        {
            DeactivateLightweightItemsControl(host);

            if (host.IsDisposed)
                return;

            RestoreLightweightScrollState(
                host,
                autoScrollMinSize,
                scrollPosition);
        }

        private static void RestoreLightweightScrollState(
            ItemsControl host,
            Size autoScrollMinSize,
            Point scrollPosition)
        {
            host.AutoScrollMinSize = autoScrollMinSize;
            host.AutoScrollPosition = new Point(
                Math.Max(0, -scrollPosition.X),
                Math.Max(0, -scrollPosition.Y));
            host.PerformLayout();
            host.Invalidate(false);
        }

        private void EnsureLightweightHostIsEligible(ItemsControl host)
        {
            EnsureLightweightHostIsEligible(host, null);
        }

        private void EnsureLightweightHostIsEligible(
            ItemsControl host,
            XmlElement declarationElement)
        {
            string message = null;
            string propertyName = "VirtualizationMode";

            if (!host.Virtualizing)
            {
                message =
                    "VirtualizationMode=Lightweight requires Virtualizing=true.";
                propertyName = "Virtualizing";
            }
            else if (!host.AutoScroll)
            {
                message =
                    "VirtualizationMode=Lightweight requires AutoScroll=true.";
                propertyName = "AutoScroll";
            }
            else if (host.Orientation != Orientation.Vertical)
            {
                message =
                    "The first lightweight backend supports only " +
                    "Orientation=Vertical.";
                propertyName = "Orientation";
            }
            else if (host.FixedItemSize <= 0)
            {
                message =
                    "VirtualizationMode=Lightweight requires a positive " +
                    "FixedItemSize.";
                propertyName = "FixedItemSize";
            }
            else if (host.TemplateRoot == null)
            {
                message =
                    "Lightweight ItemsControl requires an ItemTemplate.";
                propertyName = "ItemTemplate";
            }

            if (message == null)
                return;

            ItemTemplateActiveContext previousContext =
                PushItemTemplateDeclarationContext(host);

            try
            {
                throw LightweightMarkupError(
                    declarationElement == null
                        ? host.TemplateRoot
                        : declarationElement,
                    propertyName,
                    message);
            }
            finally
            {
                RestoreItemTemplateDeclarationContext(previousContext);
            }
        }

        private Exception RetireControlRowsForLightweight(ItemsControl host)
        {
            ArrayList records = host.RenderedItems;
            host.PublishRenderedItemRecords(new ArrayList());
            Exception firstError = null;
            int i;

            for (i = 0; records != null && i < records.Count; i++)
            {
                try
                {
                    DisposeRenderedItemRecord(
                        records[i] as RenderedItemRecord);
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(firstError, ex);
                }
            }

            return firstError;
        }

        private void ActivateLightweightItemsControl(ItemsControl host)
        {
            // A byte[] is mutable. Advance the shared decoded-image cache
            // generation once per logical lightweight refresh so the first
            // visible use validates content without rescanning on every paint.
            unchecked
            {
                _decodedImageCacheValidationGeneration++;
            }

            if (_decodedImageCacheValidationGeneration == 0)
                _decodedImageCacheValidationGeneration = 1;

            host.LightweightDisposed = false;
            host.LightweightActive = true;
            host.LightweightGeneration = NextLightweightGeneration(
                host.LightweightGeneration);
            ClearLightweightRowCache(host);

            host.LightweightLastViewportOffset = Math.Max(
                0,
                -host.AutoScrollPosition.Y);
            host.LightweightHasViewportOffset = true;
            host.LightweightOverscanDirection = 0;

            host.SetLightweightPainting(true);
            UpdateLightweightScrollExtent(host);
            UpdateLightweightVisibleRange(host);
            host.Invalidate(false);
        }

        internal void DeactivateLightweightItemsControl(ItemsControl host)
        {
            if (host == null)
                return;

            host.LightweightActive = false;
            host.LightweightRealizedStart = -1;
            host.LightweightRealizedEnd = -1;
            host.LightweightRealizedCount = 0;
            host.LightweightHotTarget = null;
            host.LightweightHasViewportOffset = false;
            host.LightweightLastViewportOffset = 0;
            host.LightweightOverscanDirection = 0;

            try
            {
                ClearLightweightRowCache(host);
            }
            finally
            {
                ClearLightweightVisitedLinks(host, true);
            }

            if (!host.IsDisposed && !host.Disposing)
            {
                host.SetLightweightPainting(false);
                host.AutoScrollMinSize = Size.Empty;
                host.UpdateScrollExtentMarker(Size.Empty, Point.Empty);
                host.Invalidate(false);
            }
        }

        internal void RestoreLightweightItemsControlAfterConfigurationFailure(
            ItemsControl host)
        {
            if (host == null || host.IsDisposed || host.Disposing ||
                host.LightweightDisposed || host.LightweightActive ||
                host.VirtualizationMode !=
                    ItemsControlVirtualizationMode.Lightweight)
            {
                return;
            }

            EnsureLightweightHostIsEligible(host);

            if (host.LightweightPlan == null)
                ValidateLightweightItemsControlConfiguration(host);

            ActivateLightweightItemsControl(host);

            if (!PrepareLightweightVisibleRows(
                    host,
                    host.RefreshGeneration))
            {
                DeactivateLightweightItemsControl(host);
            }
        }

        internal void DisposeLightweightItemsControl(ItemsControl host)
        {
            if (host == null)
                return;

            host.LightweightDisposed = true;
            Exception firstError = null;

            try
            {
                DeactivateLightweightItemsControl(host);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(
                    firstError,
                    ex);
            }

            try
            {
                DisposeLightweightTemplatePlan(host);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(
                    firstError,
                    ex);
            }

            ClearLightweightVisitedLinks(host, true);
            host.LightweightHotTarget = null;

            if (host.LightweightImageDrawAttributes != null)
            {
                try
                {
                    host.LightweightImageDrawAttributes.Dispose();
                    host.LightweightImageDrawAttributes = null;
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(
                        firstError,
                        ex);
                }
            }

            if (firstError != null)
                throw firstError;
        }

        private static void ClearLightweightVisitedLinks(
            ItemsControl host,
            bool releaseStorage)
        {
            if (host == null)
                return;

            if (host.LightweightVisitedLinks != null)
                host.LightweightVisitedLinks.Clear();

            if (host.LightweightVisitedLinkOrder != null)
                host.LightweightVisitedLinkOrder.Clear();

            if (releaseStorage)
            {
                host.LightweightVisitedLinks = null;
                host.LightweightVisitedLinkOrder = null;
            }
        }

        private void DisposeLightweightTemplatePlan(
            ItemsControl host)
        {
            LightweightTemplatePlan plan = host.LightweightPlan;
            host.LightweightPlan = null;
            Exception firstError = null;

            try
            {
                if (plan != null)
                    plan.Dispose();
            }
            catch (Exception ex)
            {
                firstError = ex;
            }

            try
            {
                ClearLightweightRowCache(host);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(firstError, ex);
            }

            try
            {
                DisposeLightweightBrushCache(host);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(firstError, ex);
            }

            if (firstError != null)
                throw firstError;
        }

        private static void DisposeLightweightBrushCache(
            ItemsControl host)
        {
            Hashtable cache = host == null
                ? null
                : host.LightweightBrushCache;

            if (cache == null)
                return;

            ArrayList brushes = new ArrayList(cache.Values);
            cache.Clear();
            host.LightweightBrushCache = null;
            Exception firstError = null;
            int i;

            for (i = 0; i < brushes.Count; i++)
            {
                IDisposable brush = brushes[i] as IDisposable;

                if (brush == null)
                    continue;

                try
                {
                    brush.Dispose();
#if !WINFORMSXAML_PACKAGE
                    host.LightweightBrushDisposeCountForTest++;
#endif
                }
                catch (Exception ex)
                {
                    firstError = FirstItemsCommitError(firstError, ex);
                }
            }

            brushes.Clear();

            if (firstError != null)
                throw firstError;
        }

        private void ClearLightweightRowCache(ItemsControl host)
        {
            Exception firstError = null;

            if (host.LightweightRowCache != null)
            {
                ArrayList snapshots = new ArrayList(
                    host.LightweightRowCache.Count);

                foreach (DictionaryEntry entry in host.LightweightRowCache)
                {
                    LightweightRowSnapshot snapshot =
                        entry.Value as LightweightRowSnapshot;

                    if (snapshot != null)
                        snapshots.Add(snapshot);
                }

                host.LightweightRowCache.Clear();

                int i;

                for (i = 0; i < snapshots.Count; i++)
                {
                    try
                    {
                        ReleaseLightweightRowSnapshot(
                            host,
                            snapshots[i] as LightweightRowSnapshot);
                    }
                    catch (Exception ex)
                    {
                        firstError = FirstItemsCommitError(
                            firstError,
                            ex);
                    }
                }
            }

            if (host.LightweightCacheEvictionKeys != null)
                host.LightweightCacheEvictionKeys.Clear();

            try
            {
                ClearLightweightThumbnailCache(host);
            }
            catch (Exception ex)
            {
                firstError = FirstItemsCommitError(firstError, ex);
            }

            if (firstError != null)
                throw firstError;
        }

        private static int NextLightweightGeneration(int generation)
        {
            return generation == Int32.MaxValue
                ? 1
                : generation + 1;
        }

    }
}
