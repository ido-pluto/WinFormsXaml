using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        public partial class ItemsControl
        {
            internal LightweightTemplatePlan LightweightPlan;
            internal bool LightweightActive;
            internal bool LightweightDisposed;
            internal int LightweightGeneration;
            internal int LightweightRealizedStart = -1;
            internal int LightweightRealizedEnd = -1;
            internal int LightweightRealizedCount;
            internal System.Collections.Hashtable LightweightRowCache;
            internal System.Collections.ArrayList LightweightCacheEvictionKeys;
            internal System.Collections.Hashtable LightweightBrushCache;
            internal System.Collections.ArrayList LightweightThumbnailCache;
            internal bool LightweightThumbnailPaintAllowed;
            internal System.Drawing.Imaging.ImageAttributes
                LightweightImageDrawAttributes;
            internal System.Collections.Hashtable LightweightVisitedLinks;
            internal System.Collections.ArrayList LightweightVisitedLinkOrder;
            internal bool LightweightHasViewportOffset;
            internal int LightweightLastViewportOffset;
            internal int LightweightOverscanDirection;
            internal LightweightHitTarget LightweightHotTarget;

#if !WINFORMSXAML_PACKAGE
            internal long LightweightBrushCreateCountForTest;
            internal long LightweightBrushDisposeCountForTest;

            internal int LightweightSharedBrushCountForTest
            {
                get
                {
                    return LightweightBrushCache == null
                        ? 0
                        : LightweightBrushCache.Count;
                }
            }
#endif

            internal void SetLightweightPainting(bool enabled)
            {
                // ItemsControl owns its normal Panel painting in every mode.
                // Only the extra buffering policy is mode-specific.
                SetStyle(ControlStyles.UserPaint, true);
                SetStyle(ControlStyles.AllPaintingInWmPaint, enabled);
                SetStyle(ControlStyles.OptimizedDoubleBuffer, enabled);
                UpdateStyles();

                if (!enabled)
                {
                    Cursor = Cursors.Default;
                    LightweightHotTarget = null;
                }
            }

            /// <summary>
            /// Raises Paint and draws the visible owner-rendered rows when the
            /// Lightweight backend is active.
            /// </summary>
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                if (LightweightActive &&
                    !LightweightDisposed &&
                    Runtime != null)
                {
                    Runtime.PaintLightweightItems(this, e);
                }
            }

            /// <summary>
            /// Raises MouseMove and updates Lightweight row hit-test feedback.
            /// </summary>
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);

                if (LightweightActive &&
                    !LightweightDisposed &&
                    Runtime != null)
                {
                    Runtime.UpdateLightweightHotTarget(this, e.Location);
                }
            }

            /// <summary>
            /// Raises MouseLeave and clears Lightweight row hit-test feedback.
            /// </summary>
            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);

                if (LightweightActive)
                {
                    LightweightHotTarget = null;
                    Cursor = Cursors.Default;
                }
            }

            /// <summary>
            /// Raises MouseUp and activates a Lightweight checkbox or hyperlink
            /// hit target after a left-button release.
            /// </summary>
            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);

                if (e.Button == MouseButtons.Left &&
                    LightweightActive &&
                    !LightweightDisposed &&
                    Runtime != null)
                {
                    Runtime.ActivateLightweightHitTarget(
                        this,
                        e.Location);
                }
            }
        }
    }
}
