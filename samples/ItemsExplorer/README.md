# ItemsExplorer sample

The first tab starts with the simple `ItemsControl` path: bind an
`ItemsBinding<T>`, declare one item template, and let stable
`PropertyBinding<T>` item fields patch the realized row. It demonstrates
collection adds, two-way row editing through `.Value`, immutable snapshot IDs,
`Tag="{Binding .}"` event context, and `ItemsBinding<T>.Replace` with a snapshot
that retains most row instances while replacing, removing, and adding only a
small number. The list publishes a bounded identity-aware diff and keeps
reorders small by retaining a longest increasing occurrence-identity
subsequence.

The second tab shows the tuning added only for a measured large list:

- 2,500 rows with stable `ItemKeyPath` and cheap `ItemVersionPath` values;
- direct synchronous fixed-size viewport virtualization;
- bounded same-item cache reuse as an optimization hint;
- a programmatic `ScrollToIndex` jump through the declaration-only
  `private ItemsControl LargeResults = null;` field, automatically wired from
  the XML `Name` before `OnLoaded`.

Compare the two XML blocks in `UI/MainForm.xml`: ordinary lists need very little
configuration, while the large-list options make identity and size guarantees
explicit so the runtime can skip work safely.

The third tab renders the same 2,500 objects through
`VirtualizationMode="Lightweight"`. Its strict Border/StackPanel/Label/
CheckBox/HyperlinkLabel template is painted on one surface, including a
TwoWay owner-drawn checkbox, without constructing a native row tree.

The fourth tab keeps native Controls but opts into
`ItemRecycling="Explicit"`. `RecyclableRowPanel` implements
`IRecyclableItemControl`, resets only state owned by that row, and then lets the
runtime patch all dynamic XAML slots for the new item. This is the contract to
use when editors or arbitrary custom controls make lightweight painting too
restrictive.
