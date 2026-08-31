using System;
using System.Collections;
using System.Windows.Forms;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        /// <summary>
        /// Releases every runtime-owned resource associated with one realized
        /// item. The record is detached before disposal so cleanup is idempotent
        /// and a later retry cannot release the same slots or control twice.
        /// </summary>
        private void DisposeRenderedItemRecord(
            RenderedItemRecord record)
        {
            if (record == null)
                return;

            ItemsControl owner = record.Owner;
            Control indexedControl = record.Control;
            record.Owner = null;

            // Binding-slot release can invoke application callbacks. Remove a
            // currently published record from the root index before crossing
            // that callback boundary, while guarding replacement records that
            // may already own the same retained Control.
            if (owner != null && indexedControl != null)
            {
                owner.UnindexRenderedItemRecord(
                    record,
                    indexedControl);
            }

            ArrayList bindingSlots = record.BindingSlots;
            record.BindingSlots = null;
            Exception cleanupError =
                ReleaseRenderBindingSlots(bindingSlots);

            record.Item = null;
            record.FunctionResults = null;
            record.VersionValue = null;

            if (record.Control == null)
            {
                if (cleanupError != null)
                    throw cleanupError;

                return;
            }

            Control control = record.Control;
            record.Control = null;

            try
            {
                if (control.Parent != null)
                    control.Parent.Controls.Remove(control);
            }
            catch (Exception ex)
            {
                cleanupError = FirstItemsCommitError(
                    cleanupError,
                    ex);
            }

            try
            {
                ReleaseElementTree(control);
            }
            catch (Exception ex)
            {
                cleanupError = FirstItemsCommitError(
                    cleanupError,
                    ex);
            }

            try
            {
                control.Dispose();
            }
            catch (Exception ex)
            {
                cleanupError = FirstItemsCommitError(
                    cleanupError,
                    ex);
            }

            if (owner != null && control.IsDisposed)
                owner.RecordItemControlTreeDisposed();

            if (cleanupError != null)
                throw cleanupError;
        }
    }
}
