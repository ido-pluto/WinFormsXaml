namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        public partial class ItemsControl
        {
            // Replacement viewport state. These fields deliberately do not
            // reuse the legacy scheduler/model fields: the direct engine is
            // synchronous and can therefore be integrated and removed as one
            // self-contained path.
            internal VirtualItemSourceAdapter DirectVirtualItemSource;
            internal System.Collections.ArrayList DirectVirtualItemValues;
            internal VirtualViewportModel DirectVirtualViewport;
            internal bool DirectVirtualActive;
            internal bool DirectVirtualRefreshRunning;
            internal bool DirectVirtualPositioningControls;
            internal int DirectVirtualRefreshOwnerGeneration;
            internal bool DirectVirtualSuppressScrollRefresh;
            internal bool DirectVirtualDisposed;
            internal int DirectVirtualGeneration;
            internal int DirectVirtualRealizedStart = -1;
            internal int DirectVirtualRealizedEnd = -1;
            internal bool DirectVirtualHasPublishedScrollAxis;
            internal int DirectVirtualLastPublishedScrollAxis;
            internal int DirectVirtualLastPublishedOverscanDirection;
            internal VirtualViewportModel DirectVirtualTranslatedFrameModel;
            internal System.Collections.ArrayList
                DirectVirtualTranslatedFrameRecords;
            internal System.Drawing.Rectangle
                DirectVirtualTranslatedFrameViewport;
            internal int DirectVirtualTranslatedFrameGeneration;
            internal bool DirectVirtualScrollExtentDeferred;
            internal System.Drawing.Size DirectVirtualDeferredNativeExtent;
            internal System.Drawing.Size DirectVirtualDeferredMarkerExtent;
            internal long DirectVirtualDeferredContentExtent;
            internal int DirectVirtualDeferredExtentGeneration;
#if !WINFORMSXAML_PACKAGE
            internal long DirectVirtualTranslationFastPathCount;
#endif

            /// <summary>
            /// Lets scroll, resize, and layout event hooks service the direct
            /// viewport without scheduling a second UI-thread operation.
            /// </summary>
            internal bool HandleDirectVirtualViewportChanged()
            {
                if (LightweightActive &&
                    !LightweightDisposed &&
                    Runtime != null)
                {
                    Runtime.HandleLightweightViewportChanged(this);
                    RetargetActiveItemScrollAfterLayout();
                    return true;
                }

                if (!DirectVirtualActive ||
                    DirectVirtualDisposed ||
                    Runtime == null)
                {
                    return false;
                }

                // A setter/layout operation owned by the direct engine is
                // already reconciling (or intentionally publishing its native
                // scroll extent). It still owns the event; the caller must not
                // fall through into the normal/legacy layout path.
                if (DirectVirtualSuppressScrollRefresh ||
                    (DirectVirtualRefreshRunning &&
                     DirectVirtualRefreshOwnerGeneration ==
                        DirectVirtualGeneration))
                {
                    return true;
                }

                try
                {
                    Runtime.RefreshDirectVirtualViewportSynchronously(
                        this,
                        false,
                        false);
                }
                catch (DirectVirtualizationIneligibleException)
                {
                    // A root that was safe in an earlier range can become
                    // collapsed through a per-item or dynamic style later. Run
                    // the ordinary keyed refresh at the current scroll position;
                    // its direct probe rejects the collapsed candidate before
                    // publication and then falls through to normal rendering.
                    Runtime.BeginItemsRefresh(this, false);
                }

                return true;
            }

            internal bool ShouldDeferDirectVirtualScrollExtent
            {
                get
                {
                    return _smoothScrollActive &&
                        HasActiveThemedScrollBar &&
                        DirectVirtualActive &&
                        !DirectVirtualDisposed;
                }
            }

            private void FlushDeferredDirectVirtualScrollExtent()
            {
                if (!DirectVirtualScrollExtentDeferred)
                    return;

                if (Runtime == null ||
                    DirectVirtualDisposed ||
                    IsDisposed ||
                    Disposing)
                {
                    ClearDeferredDirectVirtualScrollExtent();
                    return;
                }

                Runtime.FlushDeferredDirectVirtualScrollExtent(this);
            }

            internal void ClearDeferredDirectVirtualScrollExtent()
            {
                DirectVirtualScrollExtentDeferred = false;
                DirectVirtualDeferredNativeExtent =
                    System.Drawing.Size.Empty;
                DirectVirtualDeferredMarkerExtent =
                    System.Drawing.Size.Empty;
                DirectVirtualDeferredContentExtent = 0L;
                DirectVirtualDeferredExtentGeneration = 0;
            }

#if !WINFORMSXAML_PACKAGE
            internal long DirectVirtualTranslationFastPathCountForTest
            {
                get { return DirectVirtualTranslationFastPathCount; }
            }
#endif

            /// <summary>
            /// Handles ScrollToIndex for the direct viewport. A false result
            /// tells the caller to continue through the normal renderer.
            /// </summary>
            internal bool TryScrollDirectVirtualItemIntoView(int index)
            {
                return TryScrollDirectVirtualItemIntoView(
                    index,
                    ItemScrollAlignment.Start,
                    true,
                    false);
            }

            internal bool TryScrollDirectVirtualItemIntoView(
                int index,
                ItemScrollAlignment alignment,
                bool hasAnimationOverride,
                bool animate)
            {
                if (LightweightActive &&
                    !LightweightDisposed &&
                    Runtime != null)
                {
                    Runtime.ScrollLightweightItemIntoView(
                        this,
                        index,
                        alignment,
                        hasAnimationOverride,
                        animate);
                    return true;
                }

                if (!DirectVirtualActive ||
                    DirectVirtualDisposed ||
                    Runtime == null)
                {
                    return false;
                }

                Runtime.ScrollDirectVirtualItemIntoView(
                    this,
                    index,
                    alignment,
                    hasAnimationOverride,
                    animate);
                return true;
            }
        }
    }
}
