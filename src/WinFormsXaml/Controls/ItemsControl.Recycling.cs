using System;

namespace WinFormsXaml
{
    public sealed partial class XamlRuntime
    {
        public partial class ItemsControl
        {
            private ItemRecyclingMode _itemRecycling =
                ItemRecyclingMode.Disabled;
            private long _virtualCrossItemRecycleCount;
            private long _virtualCrossItemRecycleRejectedCount;

            /// <summary>
            /// Gets or sets the explicit cross-item native Control recycling
            /// policy. Disabled is the safe default and retains only same-item
            /// cache reuse. Explicit additionally requires the ItemTemplate
            /// root to implement IRecyclableItemControl for every transition.
            /// This setting has no effect in Lightweight mode.
            /// </summary>
            public ItemRecyclingMode ItemRecycling
            {
                get { return _itemRecycling; }
                set
                {
                    if (value != ItemRecyclingMode.Disabled &&
                        value != ItemRecyclingMode.Explicit)
                    {
                        throw new ArgumentOutOfRangeException(
                            "value",
                            "Unknown item recycling mode.");
                    }

                    _itemRecycling = value;
                }
            }

            /// <summary>
            /// Gets the lifetime number of detached cached Control trees that
            /// were explicitly reset and published for a different data item.
            /// This is a subset of VirtualCacheReuseCount.
            /// </summary>
            public long VirtualCrossItemRecycleCount
            {
                get { return _virtualCrossItemRecycleCount; }
            }

            /// <summary>
            /// Gets the lifetime number of explicit cross-item candidates that
            /// were discarded because the reset contract declined the change
            /// or the compiled dynamic slots required a structural rebuild.
            /// </summary>
            public long VirtualCrossItemRecycleRejectedCount
            {
                get { return _virtualCrossItemRecycleRejectedCount; }
            }

            internal void RecordVirtualCrossItemRecycleSuccess(int count)
            {
                _virtualCrossItemRecycleCount = AddDiagnosticCount(
                    _virtualCrossItemRecycleCount,
                    count);
            }

            internal void RecordVirtualCrossItemRecycleRejection()
            {
                _virtualCrossItemRecycleRejectedCount =
                    AddDiagnosticCount(
                        _virtualCrossItemRecycleRejectedCount,
                        1);
            }
        }
    }
}
