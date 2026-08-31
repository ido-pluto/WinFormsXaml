using System;
using System.Collections;
using System.Threading;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        /// <summary>
        /// Commits the current target value of every TwoWay binding for one
        /// named element property. This is the companion to
        /// UpdateSourceTrigger=Explicit and may also force an earlier commit
        /// for another TwoWay trigger.
        /// </summary>
        public void UpdateBindingSource(
            string elementName,
            string propertyName)
        {
            if (String.IsNullOrEmpty(elementName))
                throw new ArgumentNullException("elementName");

            VerifyBindingSourceUpdateThread();
            UpdateBindingSource(this[elementName], propertyName);
        }

        /// <summary>
        /// Commits the current target value of every TwoWay binding for one
        /// target object and property.
        /// </summary>
        public void UpdateBindingSource(
            object target,
            string propertyName)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (String.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException("propertyName");

            VerifyBindingSourceUpdateThread();

            ArrayList matches = new ArrayList();

            lock (_observableBindingSync)
            {
                if (_observableBindingSubscriptionsDisposed ||
                    _dynamicFeaturesDisposed)
                {
                    throw new ObjectDisposedException("XamlRuntime");
                }

                int i;

                for (i = 0;
                     _observableBindingRegistrations != null &&
                     i < _observableBindingRegistrations.Count;
                     i++)
                {
                    ObservableBindingRegistration registration =
                        _observableBindingRegistrations[i] as
                            ObservableBindingRegistration;

                    if (IsObservableBindingActiveUnderLock(registration) &&
                        registration.Mode == BindingMode.TwoWay &&
                        Object.ReferenceEquals(registration.Target, target) &&
                        ObservableRegistrationTargetsProperty(
                            registration,
                            propertyName))
                    {
                        matches.Add(registration);
                    }
                }
            }

            if (matches.Count == 0)
            {
                throw new InvalidOperationException(
                    "No active TwoWay binding targets property '" +
                    propertyName + "' on " +
                    target.GetType().FullName + ".");
            }

            int matchIndex;

            for (matchIndex = 0;
                 matchIndex < matches.Count;
                 matchIndex++)
            {
                OnObservableTargetValueChanged(
                    matches[matchIndex] as
                        ObservableBindingRegistration);
            }
        }

        private void VerifyBindingSourceUpdateThread()
        {
            if (Thread.CurrentThread.ManagedThreadId !=
                _observableOwnerThreadId)
            {
                throw new InvalidOperationException(
                    "Binding sources must be updated on the thread that " +
                    "loaded the XAML runtime.");
            }
        }
    }
}
