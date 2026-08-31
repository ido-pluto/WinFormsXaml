using System;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        public partial class ItemsControl
        {
            private long _itemTemplateBlueprintBuildCount;
            private long _itemTemplateFallbackBuildCount;
            private long _itemControlTreeDisposedCount;
#if !WINFORMSXAML_PACKAGE
            private bool _itemsLayoutScanDiagnosticsEnabled;
            private long _itemsMeasureRecordProbeCount;
            private long _itemsVisibilityFallbackProbeCount;
            private long _itemSourceReloadPostCount;
            private long _reactiveItemUpdatePostCount;
            private long _wrappedLayoutPlanAllocationCount;
            private long _wrappedLayoutArrayAllocationCount;
            private long _wrappedLayoutScratchReuseCount;
            private long _wrappedLayoutSecondPassReuseCount;
#endif

            /// <summary>
            /// Gets the lifetime number of item control trees constructed by
            /// the precompiled item-template blueprint.
            /// </summary>
            public long ItemTemplateBlueprintBuildCount
            {
                get { return _itemTemplateBlueprintBuildCount; }
            }

            /// <summary>
            /// Gets the lifetime number of item control trees constructed by
            /// the complete general-purpose item-template renderer.
            /// </summary>
            public long ItemTemplateFallbackBuildCount
            {
                get { return _itemTemplateFallbackBuildCount; }
            }

            /// <summary>
            /// Gets the lifetime number of item control trees disposed by this
            /// host after normal removal, cache eviction, or replacement.
            /// </summary>
            public long ItemControlTreeDisposedCount
            {
                get { return _itemControlTreeDisposedCount; }
            }

            /// <summary>
            /// Gets the current number of active observable binding
            /// registrations owned by this host's Controls or Lightweight
            /// item rows.
            /// </summary>
            public int ActiveItemBindingSubscriptionCount
            {
                get
                {
                    return Runtime == null
                        ? 0
                        : Runtime.CountActiveItemBindingSubscriptions(this);
                }
            }

            internal void RecordItemTemplateBlueprintBuild()
            {
                _itemTemplateBlueprintBuildCount = AddDiagnosticCount(
                    _itemTemplateBlueprintBuildCount,
                    1);
            }

            internal void RecordItemTemplateFallbackBuild()
            {
                _itemTemplateFallbackBuildCount = AddDiagnosticCount(
                    _itemTemplateFallbackBuildCount,
                    1);
            }

            internal void RecordItemControlTreeDisposed()
            {
                _itemControlTreeDisposedCount = AddDiagnosticCount(
                    _itemControlTreeDisposedCount,
                    1);
            }

#if !WINFORMSXAML_PACKAGE
            internal bool ItemsLayoutScanDiagnosticsEnabled
            {
                get { return _itemsLayoutScanDiagnosticsEnabled; }
            }

            internal long ItemsMeasureRecordProbeCountForTest
            {
                get { return _itemsMeasureRecordProbeCount; }
            }

            internal long ItemsVisibilityFallbackProbeCountForTest
            {
                get { return _itemsVisibilityFallbackProbeCount; }
            }

            internal long ItemSourceReloadPostCountForTest
            {
                get { return _itemSourceReloadPostCount; }
            }

            internal long ReactiveItemUpdatePostCountForTest
            {
                get { return _reactiveItemUpdatePostCount; }
            }

            internal long WrappedLayoutPlanAllocationCountForTest
            {
                get { return _wrappedLayoutPlanAllocationCount; }
            }

            internal long WrappedLayoutArrayAllocationCountForTest
            {
                get { return _wrappedLayoutArrayAllocationCount; }
            }

            internal long WrappedLayoutScratchReuseCountForTest
            {
                get { return _wrappedLayoutScratchReuseCount; }
            }

            internal long WrappedLayoutSecondPassReuseCountForTest
            {
                get { return _wrappedLayoutSecondPassReuseCount; }
            }

            internal object WrappedLayoutScratchIdentityForTest
            {
                get { return WrappedLayoutScratchPlan; }
            }

            internal void ResetWrappedLayoutStorageDiagnosticsForTest()
            {
                _wrappedLayoutPlanAllocationCount = 0L;
                _wrappedLayoutArrayAllocationCount = 0L;
                _wrappedLayoutScratchReuseCount = 0L;
                _wrappedLayoutSecondPassReuseCount = 0L;
            }

            internal void RecordWrappedLayoutStorageForTest(
                bool reusedPlan,
                int arrayAllocationCount,
                bool secondPassReuse)
            {
                if (reusedPlan)
                {
                    _wrappedLayoutScratchReuseCount = AddDiagnosticCount(
                        _wrappedLayoutScratchReuseCount,
                        1);
                }
                else
                {
                    _wrappedLayoutPlanAllocationCount = AddDiagnosticCount(
                        _wrappedLayoutPlanAllocationCount,
                        1);
                }

                _wrappedLayoutArrayAllocationCount = AddDiagnosticCount(
                    _wrappedLayoutArrayAllocationCount,
                    arrayAllocationCount);

                if (secondPassReuse)
                {
                    _wrappedLayoutSecondPassReuseCount = AddDiagnosticCount(
                        _wrappedLayoutSecondPassReuseCount,
                        1);
                }
            }

            internal void ResetItemUpdatePostDiagnosticsForTest()
            {
                _itemSourceReloadPostCount = 0L;
                _reactiveItemUpdatePostCount = 0L;
            }

            internal void RecordItemSourceReloadPostForTest()
            {
                _itemSourceReloadPostCount = AddDiagnosticCount(
                    _itemSourceReloadPostCount,
                    1);
            }

            internal void RecordReactiveItemUpdatePostForTest()
            {
                _reactiveItemUpdatePostCount = AddDiagnosticCount(
                    _reactiveItemUpdatePostCount,
                    1);
            }

            internal void ResetItemsLayoutScanDiagnosticsForTest()
            {
                _itemsMeasureRecordProbeCount = 0L;
                _itemsVisibilityFallbackProbeCount = 0L;
                _itemsLayoutScanDiagnosticsEnabled = true;
            }

            internal void RecordItemsMeasureRecordProbe()
            {
                _itemsMeasureRecordProbeCount = AddDiagnosticCount(
                    _itemsMeasureRecordProbeCount,
                    1);
            }

            internal void RecordItemsVisibilityFallbackProbe()
            {
                _itemsVisibilityFallbackProbeCount = AddDiagnosticCount(
                    _itemsVisibilityFallbackProbeCount,
                    1);
            }
#endif
        }

        private int CountActiveItemBindingSubscriptions(ItemsControl host)
        {
            if (host == null)
                return 0;

            lock (_observableBindingSync)
            {
                int count = 0;
                int i;

                for (i = 0;
                     _observableBindingRegistrations != null &&
                     i < _observableBindingRegistrations.Count;
                     i++)
                {
                    ObservableBindingRegistration registration =
                        _observableBindingRegistrations[i]
                            as ObservableBindingRegistration;

                    if (registration == null || !registration.Active)
                        continue;

                    RenderBindingSlot slot =
                        registration.Owner as RenderBindingSlot;
                    LightweightRowSnapshot snapshot =
                        registration.Owner as LightweightRowSnapshot;
                    bool belongsToHost =
                        (slot != null &&
                         Object.ReferenceEquals(slot.Host, host)) ||
                        (snapshot != null &&
                         !snapshot.Retired &&
                         Object.ReferenceEquals(snapshot.Host, host));

                    if (!belongsToHost)
                        continue;

                    if (count == Int32.MaxValue)
                        return count;

                    count++;
                }

                return count;
            }
        }
    }
}
