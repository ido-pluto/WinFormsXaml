using System;
using System.Collections.Generic;

namespace WinFormsXaml
{
    internal interface IPropertyBindingRuntime
    {
        Type ValueType { get; }

        event EventHandler ValueChanged;

        object GetSnapshot(out long version);

        bool TrySetSnapshot(long expectedVersion, object value);

        void SetValue(object value);
    }

    /// <summary>
    /// Stores a value and notifies listeners when that value changes.
    /// WinFormsXaml bindings unwrap this type automatically.
    /// </summary>
    public sealed class PropertyBinding<T> : IPropertyBindingRuntime
    {
        private readonly object _sync;
        private T _value;
        private long _version;
        private EventHandler _valueChanged;
        private Delegate[] _valueChangedSubscribers;

        /// <summary>
        /// Creates a binding value initialized to <c>default(T)</c>.
        /// </summary>
        public PropertyBinding()
            : this(default(T))
        {
        }

        /// <summary>Creates a binding value with an initial value.</summary>
        /// <param name="value">The initial value exposed to bindings.</param>
        public PropertyBinding(T value)
        {
            _sync = new object();
            _value = value;
            _version = 0;
        }

        /// <summary>
        /// Gets or sets the current value. Unequal assignments raise
        /// <see cref="ValueChanged"/> after the thread-safe value update.
        /// </summary>
        public T Value
        {
            get
            {
                lock (_sync)
                    return _value;
            }
            set
            {
                Delegate[] subscribers;

                lock (_sync)
                {
                    if (EqualityComparer<T>.Default.Equals(
                        _value,
                        value))
                    {
                        return;
                    }

                    _value = value;

                    unchecked
                    {
                        _version++;
                    }

                    subscribers = _valueChangedSubscribers;
                }

                if (subscribers != null)
                    DispatchValueChanged(subscribers);
            }
        }

        /// <summary>
        /// Occurs synchronously after <see cref="Value"/> changes.
        /// </summary>
        public event EventHandler ValueChanged
        {
            add
            {
                lock (_sync)
                {
                    EventHandler next =
                        (EventHandler)Delegate.Combine(
                            _valueChanged,
                            value);

                    if (!Object.ReferenceEquals(_valueChanged, next))
                    {
                        Delegate[] subscribers =
                            next == null
                                ? null
                                : next.GetInvocationList();

                        _valueChanged = next;
                        _valueChangedSubscribers =
                            subscribers;
                    }
                }
            }
            remove
            {
                lock (_sync)
                {
                    EventHandler next =
                        (EventHandler)Delegate.Remove(
                            _valueChanged,
                            value);

                    if (!Object.ReferenceEquals(_valueChanged, next))
                    {
                        Delegate[] subscribers =
                            next == null
                                ? null
                                : next.GetInvocationList();

                        _valueChanged = next;
                        _valueChangedSubscribers =
                            subscribers;
                    }
                }
            }
        }

        internal object GetSnapshot(out long version)
        {
            lock (_sync)
            {
                version = _version;
                return _value;
            }
        }

        internal bool TrySetSnapshot(long expectedVersion, object value)
        {
            Delegate[] subscribers;

            lock (_sync)
            {
                if (_version != expectedVersion)
                    return false;

                T converted = (T)value;

                if (EqualityComparer<T>.Default.Equals(
                        _value,
                        converted))
                {
                    // A successful compare-and-set is also an ordering claim.
                    // Advance the internal stamp even when the value is equal so
                    // an older two-way edit captured at the same version cannot
                    // commit after this newer edit. No value-change event is raised.
                    unchecked
                    {
                        _version++;
                    }

                    return true;
                }

                _value = converted;

                unchecked
                {
                    _version++;
                }

                subscribers = _valueChangedSubscribers;
            }

            if (subscribers != null)
                DispatchValueChanged(subscribers);

            return true;
        }

        private void DispatchValueChanged(Delegate[] subscribers)
        {
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

        Type IPropertyBindingRuntime.ValueType
        {
            get { return typeof(T); }
        }

        object IPropertyBindingRuntime.GetSnapshot(out long version)
        {
            return GetSnapshot(out version);
        }

        bool IPropertyBindingRuntime.TrySetSnapshot(
            long expectedVersion,
            object value)
        {
            return TrySetSnapshot(expectedVersion, value);
        }

        void IPropertyBindingRuntime.SetValue(object value)
        {
            Value = (T)value;
        }
    }
}
