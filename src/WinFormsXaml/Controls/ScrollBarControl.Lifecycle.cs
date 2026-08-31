using System;

namespace WinFormsXaml
{
    public abstract partial class ScrollBarControl
    {
        /// <summary>Cancels interaction and repaints when enabled changes.</summary>
        /// <param name="e">The event data.</param>
        protected override void OnEnabledChanged(EventArgs e)
        {
            if (!Enabled)
                ReleaseInteraction(false);

            base.OnEnabledChanged(e);
            Invalidate();
        }

        /// <summary>Cancels interaction when the control is hidden.</summary>
        /// <param name="e">The event data.</param>
        protected override void OnVisibleChanged(EventArgs e)
        {
            if (!Visible)
                ReleaseInteraction(false);

            base.OnVisibleChanged(e);
        }

        /// <summary>Repaints when horizontal direction changes.</summary>
        /// <param name="e">The event data.</param>
        protected override void OnRightToLeftChanged(EventArgs e)
        {
            base.OnRightToLeftChanged(e);
            Invalidate();
        }

        /// <summary>Cancels active interaction before handle destruction.</summary>
        /// <param name="e">The event data.</param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            ReleaseInteraction(false);
            base.OnHandleDestroyed(e);
        }

        /// <summary>Releases the repeat timer and style subscription.</summary>
        /// <param name="disposing">
        /// true to release managed resources; otherwise false.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseInteraction(false);
                DisposePaintResources();

                if (_repeatTimer != null)
                {
                    _repeatTimer.Tick -=
                        new EventHandler(RepeatTimerTick);
                    _repeatTimer.Dispose();
                    _repeatTimer = null;
                }

                if (_style != null)
                {
                    _style.Changed -=
                        new EventHandler(ScrollBarStyleChanged);
                }
            }

            base.Dispose(disposing);
        }
    }
}
