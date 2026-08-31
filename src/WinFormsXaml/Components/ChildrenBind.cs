using System;
using System.Collections;
using System.Windows.Forms;

namespace WinFormsXaml
{
    internal interface IChildrenBindHost
    {
        void ReplaceChildren(
            ChildrenBind owner,
            Control[] children);

        Control WrapChildren(
            ChildrenBind owner,
            Control wrapper);
    }

    /// <summary>
    /// Exposes the caller-provided controls projected through a component's
    /// <c>&lt;Children /&gt;</c> slot.
    /// </summary>
    public sealed class ChildrenBind : IEnumerable
    {
        private static readonly Control[] _emptyControls =
            new Control[0];

        private readonly object _sync = new object();
        private IChildrenBindHost _host;
        private Control[] _children = _emptyControls;
        private Control[] _pendingChildren;
        private bool _hasPendingReplacement;
        private bool _raisingChanged;
        private bool _retired;

        /// <summary>
        /// Raised after the direct projected-child collection changes.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>Gets the number of direct projected controls.</summary>
        public int Count
        {
            get
            {
                lock (_sync)
                    return _children.Length;
            }
        }

        /// <summary>Gets one direct projected control.</summary>
        public Control this[int index]
        {
            get
            {
                lock (_sync)
                    return _children[index];
            }
        }

        /// <summary>
        /// Returns a stable snapshot of the direct projected controls.
        /// </summary>
        public Control[] ToArray()
        {
            lock (_sync)
                return CloneControls(_children);
        }

        /// <summary>
        /// Finds a named control in the projected child trees.
        /// </summary>
        public T Get<T>(string name) where T : class
        {
            if (String.IsNullOrEmpty(name) || name.Trim().Length == 0)
            {
                throw new ArgumentException(
                    "A projected child name is required.",
                    "name");
            }

            Control[] snapshot = ToArray();
            object match = null;
            int i;

            for (i = 0; i < snapshot.Length; i++)
            {
                FindNamedControl(
                    snapshot[i],
                    name,
                    ref match);
            }

            if (match == null)
            {
                throw new InvalidOperationException(
                    "No projected child named '" +
                    name +
                    "' exists.");
            }

            T typed = match as T;

            if (typed == null)
            {
                throw new InvalidCastException(
                    "Projected child '" +
                    name +
                    "' is " +
                    match.GetType().FullName +
                    ", not " +
                    typeof(T).FullName +
                    ".");
            }

            return typed;
        }

        /// <summary>
        /// Replaces the direct projected controls. Ownership of every supplied
        /// control transfers to the component after an attached operation
        /// succeeds. A replacement staged before attachment remains caller-owned
        /// until the XML slot is attached successfully.
        /// </summary>
        public void Replace(params Control[] children)
        {
            Control[] replacement = ValidateAndClone(children);
            IChildrenBindHost host;

            lock (_sync)
            {
                ThrowIfRetired();
                ThrowIfRaisingChanged();
                host = _host;

                if (host == null)
                {
                    _pendingChildren = replacement;
                    _hasPendingReplacement = true;
                    _children = replacement;
                }
            }

            if (host == null)
            {
                RaiseChanged();
                return;
            }

            host.ReplaceChildren(this, replacement);
        }

        /// <summary>
        /// Removes the projected controls. Attached component-owned controls
        /// are disposed after the update commits; a replacement staged before
        /// attachment remains caller-owned.
        /// </summary>
        public void Clear()
        {
            Replace(_emptyControls);
        }

        /// <summary>
        /// Replaces the direct slot contents with <paramref name="wrapper"/>
        /// and reparents the previous controls inside that wrapper.
        /// </summary>
        public T Wrap<T>(T wrapper) where T : Control
        {
            if (wrapper == null)
                throw new ArgumentNullException("wrapper");

            IChildrenBindHost host;

            lock (_sync)
            {
                ThrowIfRetired();
                ThrowIfRaisingChanged();
                host = _host;
            }

            if (host == null)
            {
                throw new InvalidOperationException(
                    "Projected children can be wrapped only after the component " +
                    "has been attached to its XML control tree.");
            }

            return (T)host.WrapChildren(this, wrapper);
        }

        /// <summary>
        /// Enumerates a stable snapshot of the direct projected controls.
        /// </summary>
        public IEnumerator GetEnumerator()
        {
            return ToArray().GetEnumerator();
        }

        internal void Attach(
            IChildrenBindHost host,
            Control[] initialChildren)
        {
            if (host == null)
                throw new ArgumentNullException("host");

            Control[] pending = null;

            lock (_sync)
            {
                ThrowIfRetired();

                if (_host != null && !Object.ReferenceEquals(_host, host))
                {
                    throw new InvalidOperationException(
                        "A ChildrenBind instance cannot be shared by component instances.");
                }

                _host = host;
                _children = ValidateAndClone(initialChildren);

                if (_hasPendingReplacement)
                {
                    pending = CloneControls(_pendingChildren);
                    _pendingChildren = null;
                    _hasPendingReplacement = false;
                }
            }

            if (pending != null)
                host.ReplaceChildren(this, pending);
            else
                RaiseChanged();
        }

        internal void Publish(
            IChildrenBindHost host,
            Control[] children)
        {
            lock (_sync)
            {
                if (_retired || !Object.ReferenceEquals(_host, host))
                    return;

                _children = ValidateAndClone(children);
            }

            RaiseChanged();
        }

        internal void Retire(IChildrenBindHost host)
        {
            lock (_sync)
            {
                if (_host != null && !Object.ReferenceEquals(_host, host))
                    return;

                _host = null;
                _children = _emptyControls;
                _pendingChildren = null;
                _hasPendingReplacement = false;
                _retired = true;
            }
        }

        private void ThrowIfRetired()
        {
            if (_retired)
            {
                throw new ObjectDisposedException(
                    typeof(ChildrenBind).FullName,
                    "The component that owns these projected children was released.");
            }
        }

        private void ThrowIfRaisingChanged()
        {
            if (_raisingChanged)
            {
                throw new InvalidOperationException(
                    "Projected children cannot be changed recursively from " +
                    "their own Changed notification.");
            }
        }

        private static Control[] ValidateAndClone(Control[] controls)
        {
            if (controls == null)
                throw new ArgumentNullException("children");

            Control[] copy = CloneControls(controls);
            int i;

            for (i = 0; i < copy.Length; i++)
            {
                if (copy[i] == null)
                {
                    throw new ArgumentException(
                        "Projected children cannot contain null controls.",
                        "children");
                }

                int n;

                for (n = 0; n < i; n++)
                {
                    if (Object.ReferenceEquals(copy[n], copy[i]))
                    {
                        throw new ArgumentException(
                            "The same Control cannot appear more than once in projected children.",
                            "children");
                    }
                }
            }

            return copy;
        }

        private static Control[] CloneControls(Control[] controls)
        {
            if (controls == null || controls.Length == 0)
                return _emptyControls;

            Control[] copy = new Control[controls.Length];
            Array.Copy(controls, copy, controls.Length);
            return copy;
        }

        private static void FindNamedControl(
            Control control,
            string name,
            ref object match)
        {
            if (control == null)
                return;

            if (String.Equals(
                    control.Name,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (match != null && !Object.ReferenceEquals(match, control))
                {
                    throw new InvalidOperationException(
                        "Projected child name '" +
                        name +
                        "' is ambiguous inside this component instance.");
                }

                match = control;
            }

            int i;

            for (i = 0; i < control.Controls.Count; i++)
            {
                FindNamedControl(
                    control.Controls[i],
                    name,
                    ref match);
            }
        }

        private void RaiseChanged()
        {
            EventHandler handler;

            lock (_sync)
            {
                ThrowIfRaisingChanged();
                _raisingChanged = true;
                handler = Changed;
            }

            try
            {
                if (handler == null)
                    return;

                Delegate[] subscribers = handler.GetInvocationList();
                Exception firstError = null;
                int i;

                for (i = 0; i < subscribers.Length; i++)
                {
                    try
                    {
                        ((EventHandler)subscribers[i])(
                            this,
                            EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        if (firstError == null)
                            firstError = ex;
                    }
                }

                if (firstError != null)
                    throw firstError;
            }
            finally
            {
                lock (_sync)
                    _raisingChanged = false;
            }
        }
    }
}
