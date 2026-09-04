# ItemsControl: from a first list to large data sets

`ItemsControl` repeats one XML template for every object in an `IEnumerable`.
Bind `ItemsSource` to `ItemsBinding<T>` for automatic collection refreshes. Its
defaults provide incremental updates and progressive rendering. Viewport
virtualization is deliberately opt-in: add `Virtualizing="true"` only after the
ordinary renderer is correct for the list.

The C# code-behind snippets use the protected `Ui` runtime inherited from
`XmlForm`. `ItemsControl` is a top-level package type, so a named item host can
also be wired directly into a declaration-only code-behind field:

```csharp
private ItemsControl Results = null;
```

No `XamlRuntime.ItemsControl` alias or manual `Get` call is needed for this
common case. The top-level type is not sealed, so specialized item hosts may
derive from it when composition is not enough.

## Your first list

```xml
<ItemsControl Name="Results"
              ItemsSource="{Binding Results}">
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}"
           AutoSize="true"
           Margin="0,0,0,6" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`ItemsControl.ItemTemplate` is the sole item-template property element. Keep it
directly under `ItemsControl`, and put its one visual root directly inside it;
additional template wrappers and alternate template property names are rejected.

`Resources` declared inside the template are local to that template subtree.
They inherit resources visible where the template is declared, but named and
implicit styles do not leak into the Form or another item host. A nested
`ItemsControl` keeps the effective resource scope even when its rows are loaded
later. Inline, file, and embedded `<Presets>` declarations in a template are
imported once for that template rather than once per realized row.

```csharp
public sealed class SearchResult
{
    public readonly PropertyBinding<string> Title;

    public SearchResult(string title)
    {
        Title = new PropertyBinding<string>(title);
    }
}

public readonly ItemsBinding<SearchResult> Results =
    new ItemsBinding<SearchResult>();
```

Populate the list before loading the XML, or mutate it later:

```csharp
Results.Add(new SearchResult("First result"));
Results.Add(new SearchResult("Second result"));

// After the XML has loaded, this schedules one incremental refresh.
Results.Add(new SearchResult("Third result"));
```

Each binding with omitted `Source` inside the template reads from the current
item. A stable `PropertyBinding<T>` item field updates its realized controls
without a manual reload:

```csharp
Results[0].Title.Value = "Updated title";
```

Shared Form state remains available without adding it to every row. Select the
code-behind source for only the binding that needs it:

```xml
<Button Text="{Binding Title}"
        Enabled="{Binding CanOpenResults, Source=CodeBehind}" />
```

`Source=Current` is the explicit form of the default item source. A
`PropertyBinding<T>` on the code-behind updates all dependent realized rows,
including interpolated values and bindings reactivated from the virtual cache.
Those subscriptions are pooled and detach when rows or the runtime are
discarded. A condition on the item-template root selects the normal keyed
renderer, where every row remains observable and can reappear later.

The runtime coalesces a wrapper change by item and target slot and patches only
that realized property. It does not re-enumerate the item source or read sibling
items. Changes that can
alter structure—such as a template-root `Condition`, a component boundary, or
an unavailable target—use the full incremental refresh path instead.

## Two-way editors inside a template

The same reactive-wrapper rule applies inside each realized item template:

```xml
<ItemsControl Name="EditableResults"
              ItemsSource="{Binding Results}">
  <ItemsControl.ItemTemplate>
    <TextBox Text="{Binding Title, Mode=TwoWay}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

Editing a row assigns that row's `Title.Value`. `PropertyBinding<T>`
dependencies also work in direct and interpolated item expressions. When
controls are patched or reused, their subscriptions remain attached to the
current item. Cached virtual records detach their subscriptions and reactivate
them only when that same item is realized again; discarded records detach
permanently.

A complete two-way binding may also target shared code-behind state:

```xml
<TextBox Text="{Binding SharedQuery, Mode=TwoWay, Source=CodeBehind}" />
```

An edit updates the Form's `PropertyBinding<T>` and refreshes its other active
consumers.

Two-way item bindings use the same native properties and reversible aliases as
global bindings. They must be one complete direct binding ending in a writable
`PropertyBinding<T>` on a realized target. Existing writable
notification-based properties remain compatible. Negation, interpolation,
template conditions, styles, attached properties, `ItemsSource`, and values whose
change requires rebuilding a component or style tree are rejected.

## A practical result card

A template can contain any supported controls and layout containers:

```xml
<ItemsControl Name="Results">
  <ItemsControl.ItemTemplate>
    <Border Padding="10"
            Margin="0,0,0,8"
            BorderBrush="#D0D0D0"
            BorderThickness="1">
      <StackPanel>
        <Label Text="{Binding Title}"
               AutoSize="true" />
        <Label Text="{Binding Description}"
               AutoSize="true"
               Margin="0,3,0,6" />
        <Button Text="Open"
                Tag="{Binding .}"
                Click="OpenResult_Click" />
      </StackPanel>
    </Border>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`Tag="{Binding .}"` stores the complete current item on the button:

```csharp
private void OpenResult_Click(object sender, EventArgs e)
{
    Button button = (Button)sender;
    SearchResult result = (SearchResult)button.Tag;
    OpenResult(result);
}
```

The event method belongs to the normal form code-behind object. Template
bindings still belong to each item.

## Nested item properties

Paths can walk through public properties and fields:

```xml
<ItemsControl Name="Orders">
  <ItemsControl.ItemTemplate>
    <Grid Margin="0,0,0,6">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="100" />
      </Grid.ColumnDefinitions>

      <Label Grid.Column="0"
             Text="{Binding Customer.DisplayName}" />
      <Label Grid.Column="1"
             Text="{Binding TotalText}" />
    </Grid>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

When `Customer` or `DisplayName` is a stable `PropertyBinding<T>`, the runtime
observes that segment. Replacing its `Value` re-resolves the remaining path,
updates only the affected realized row, and detaches the obsolete branch.

## Observe, replace, or manually reload a source

`ItemsBinding<T>` derives from the .NET 2.0 `BindingList<T>`. `Add`, `Insert`,
`Remove`, `RemoveAt`, and `Clear` publish collection changes. `AddRange` disables
per-item notifications while adding and publishes at most one reset. It first
snapshots the input, so passing the list itself is safe and a source-enumeration
failure cannot leave a partially appended range:

```csharp
Results.AddRange(Search(Query.Value));
```

The `ItemsBinding<T>(IList<T>)` constructor also snapshots its input. The new
binding owns its observable list state; later edits and `Replace` calls never
mutate the caller-owned list that supplied the initial values.

Use `Replace` when a search, refresh, or API call returns the complete next
list:

```csharp
Results.Replace(Search(Query.Value));
```

`Replace` treats the list itself as an immediate no-op and snapshots every
other source before touching the live list. A failed source enumeration leaves
the existing list and its notifications unchanged. Any identical sequence
publishes nothing. Small changes are reconciled as precise `ItemAdded`, `ItemDeleted`,
`ItemChanged`, or `ItemMoved` notifications, including duplicate items. Reorders
retain a longest increasing subsequence of occurrence identities, so a one-move
rotation does not become a long move series. This lets the runtime retain
unaffected rows and item subscriptions when it has a usable item key. A large,
unrelated replacement is deliberately bounded and falls back to one `Reset`
instead of spending unbounded time calculating a detailed edit script.

Reference-type items are matched by object identity; value-type items use their
normal value equality. This avoids merging two distinct model objects merely
because they implement value-based `Equals`.

`Replace` detects structural changes, not invisible mutations inside the same
object. Use stable `PropertyBinding<T>` item fields so a changed value updates
its realized slots automatically. For a non-notifying snapshot object mutated
in place, refresh just that occurrence through the binding:

```csharp
int index = Results.IndexOf(result);
result.Status = "Connected";
Results.ReloadItem(index);
```

`ReloadItem(index)` publishes one precise item-change notification. In the
common non-virtual keyed path, each observing `ItemsControl` re-evaluates only
that row, including its `{Function ...}` values, and patches compatible controls
in place without changing the row control's identity. The index is the current
zero-based logical index in the `ItemsBinding<T>`, not a realized-control index
after conditions or virtualization. Templates that require rebuilding, active
virtualization, or an unverifiable source state continue through the normal
transactional refresh fallback. The method rejects an index outside the current
list instead of publishing a malformed notification.

Use `ReloadItems()` when external state can affect every row:

```csharp
_iconCache.Clear();
Results.ReloadItems();
```

This publishes one reset notification. The renderer re-evaluates the current
view, but its normal keyed diff still retains and patches compatible control
trees. If the same `ItemsBinding<T>` is displayed by multiple item hosts, all
of them receive either reload notification. By contrast,
`Ui.ReloadItems("Results")` targets only that named host and remains useful for
an ordinary non-observable `IEnumerable`.

The binding can also bring an item to the user without looking up a named
control:

```csharp
Results.ScrollIntoView(result);
Results.ScrollIntoView(result, ItemScrollAlignment.Center, true);
Results.ScrollIndexIntoView(duplicateIndex, ItemScrollAlignment.End, false);
```

`ScrollIntoView(item)` deterministically selects the first occurrence accepted
by `EqualityComparer<T>.Default`. Use `ScrollIndexIntoView` when duplicates
must be distinguished, or when `T` itself is `int`. A binding request is
broadcast to every `ItemsControl` currently observing that binding. The
observers are weak and are detached on source replacement and disposal. To
move only one view of a shared binding, call `ScrollIntoView` on that specific
host instead.

The collection update and scroll are ordered transactionally for each host:

```csharp
Results.Add(result);
Results.ScrollIntoView(result, ItemScrollAlignment.Center, true);
```

The request waits for the queued row patch to commit, then the item overload
resolves the first current equal occurrence. If another change removes the item
before commit, scrolling safely becomes a no-op instead of using its stale
index. `ScrollIndexIntoView` deliberately retains the numeric index and checks
it against the final committed snapshot. A burst from a worker thread posts at
most one pending dispatch per host and keeps only the newest destination.

Item-aware animation retains the logical item and alignment as its authority.
If a wrapped list reflows during resize, or Controls virtualization measures a
different item height than its estimate, the running transition is retargeted
from its current position and finishes at the new `Start`, `Center`, or `End`.

These explicit item reloads also detect content edits made in place to an
encoded `byte[]` used by an item-template `Image.Source` or
`PictureBox.Source`. The compatible row controls stay in place, one replacement
bitmap is shared by controls using that array, and the previous generated
bitmap is released after its last assignment moves to the replacement.

When the host uses `ItemVersionPath`, increment that version before
`ReloadItem`; an unchanged version is an explicit promise that ordinary bound
values did not change. Functions are still recalculated when
`ReevaluateFunctionsOnRefresh` is true, which is the default.

`ItemsControl` observes any `IBindingList` whose
`SupportsChangeNotification` property is true, not only `ItemsBinding<T>`.
Bursts of `ListChanged` events are coalesced into one owner-thread update. A
bounded, well-formed add/delete/move/change batch is replayed against the
committed snapshot and checked against the final `IBindingList`, avoiding source
enumeration. The exact snapshot then continues through the normal transactional
keyed planner so control order, conditions, virtualization, subscriptions,
progressive work, and rollback retain one authority. Reset, oversized,
malformed, stale, or otherwise unverifiable batches fall back to a full source
reload. A notification received before a handle exists is kept as pending work
and activated after `HandleCreated`.

The notification is safe to raise from another thread because the runtime
marshals the refresh. The collection implementation still owns its own thread
safety; `ItemsBinding<T>` does not make concurrent reads and writes safe by
itself.

`ItemsSource` is one-way. To replace the complete source reactively from an
`XmlForm`, expose a stable wrapper:

```csharp
using System.Collections;

public readonly PropertyBinding<IEnumerable> ResultsSource =
    new PropertyBinding<IEnumerable>();

private void ReplaceResultsSource()
{
    ResultsSource.Value = Search(Query.Value);
}
```

```xml
<ItemsControl Name="Results"
              ItemsSource="{Binding ResultsSource}">
  <!-- ItemTemplate -->
</ItemsControl>
```

Do not use `Mode=TwoWay` on `ItemsSource`; edit the observable collection or
assign `ResultsSource.Value` instead.

An ordinary `IEnumerable` is also valid, but it has no change notification. Use
the existing manual APIs for that case:

```csharp
Ui.SetItems("Results", results);       // assign and render

results.Add(new SearchResult("New result"));
Ui.ReloadItems("Results");             // re-enumerate current source

Ui.ClearItems("Results");              // clear source and visuals
```

The runtime compares the new view with the committed view and reuses or patches
compatible control trees. A declarative source is not rendered until XML
post-configuration, after the complete `ItemTemplate` has been parsed. Replacing
the source detaches the old list, stale notifications are ignored, and disposal
removes the active subscription. If enumeration or rendering fails, the last
committed source and controls remain visible.

## Conditional rows

Put `Condition` on the template root to include or exclude the complete item:

```xml
<ItemsControl Name="Messages">
  <ItemsControl.ItemTemplate>
    <StackPanel Condition="{Binding IsVisible}"
                Margin="0,0,0,6">
      <Label Text="{Binding SenderName}" />
      <Label Text="{Binding Preview}" />
    </StackPanel>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

A false root condition consumes no list space. Negation uses the normal binding
form:

```xml
<Label Condition="{Binding !IsArchived}"
       Text="{Binding Title}" />
```

When `IsVisible` or `IsArchived` is a `PropertyBinding<bool>`, changing its
`Value` refreshes membership automatically. A false dynamic root is retained as
collapsed by the normal keyed renderer so its subscription can make the row
reappear later. Root membership is not compatible with the direct logical-index
viewport: a root `Condition` therefore selects the normal renderer even when
`Virtualizing="true"`. This keeps the semantics exact, at the cost of realizing
all row roots. Put a condition on a descendant instead when the outer row must
keep a stable slot and remain eligible for direct virtualization.

Snapshot boolean fields and functions without an explicit reactive path argument
do not publish changes. After changing one of those, call
`ReloadItems`. `Condition` is one-way and rejects `Mode=TwoWay`. Root
`Visibility` and all layered component/template conditions also apply; the row
is shown only when every constraint permits it.

## Functions in a template

Use a function when display data needs code rather than a simple property path:

```xml
<ItemsControl Name="Files">
  <ItemsControl.ItemTemplate>
    <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
      <PictureBox Image="{Function GetFileIcon(.)}"
                  SizeMode="CenterImage" />
      <Label Text="{Function FormatFileName(Name, Size)}" />
    </StackPanel>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

```csharp
public Image GetFileIcon(object item)
{
    FileRow row = (FileRow)item;
    return _icons.GetForExtension(row.Extension);
}

public string FormatFileName(string name, long size)
{
    return name + " (" + size.ToString() + " bytes)";
}
```

Functions are called on the form code-behind object. `.` passes the current
item. Explicit paths such as `Name` and `Size` resolve against that current item
and automatically re-run the function when their `PropertyBinding<T>` values
change.
Function return values can be real CLR objects such as `Image`.

## Preset values in a template

Item-template preset attributes use the same rendering rule as ordinary control
attributes: the runtime checks the selected preset, then its configured default.
It does not scan other variants. If neither supplies the key, the attribute is
unresolved and makes no assignment, so a newly realized control keeps its normal
system/framework property baseline.

When a key that previously resolved becomes unresolved, the retained item
control is not removed or rebuilt. Its affected property returns to the captured
baseline; if the key resolves later, that same control receives the new value.
This is different from an invalid item `Binding` path: missing binding members
still produce the source-located refresh failures described below.

## Observe completion and errors

Rendering may continue in small message-loop batches. Subscribe when other UI
depends on completion:

```csharp
private void ConnectResultsEvents()
{
    Ui.GetItemsControl("Results").RefreshCompleted +=
        Results_RefreshCompleted;

    Ui.GetItemsControl("Results").RefreshFailed +=
        Results_RefreshFailed;
}

private void Results_RefreshCompleted(object sender, EventArgs e)
{
    StatusText.Value = "Results updated";
}

private void Results_RefreshFailed(object sender, EventArgs e)
{
    Exception error = Ui.GetItemsControl("Results").LastRefreshError;
    StatusText.Value = error == null ? "Refresh failed" : error.Message;
}
```

On an enumeration, planning, binding, function, or template-build failure, the
last committed source and controls stay visible. Starting a newer refresh
cancels unfinished work from the older one without reporting it as a failure.
When `LastRefreshError` is a `WinFormsXamlLoadException` originating in the item
template, its semantic line and position still refer to the original
`ItemTemplate` attribute, or to the deepest opening element when that attribute
is no longer retained. A failure inside a registered component instead retains
that component resource's coordinates. Template compilation and per-item
cloning never replace either source with generated XML locations.

## What the defaults optimize automatically

Without extra attributes, `ItemsControl`:

- enables `AutoScroll`;
- reuses compatible item controls;
- looks for a conventional `Id`, `ID`, `_id`, or `Key` member;
- renders changed controls in small UI-thread batches on the normal path;
- enables smooth scrolling for wheel, arrow, and page commands;
- keeps viewport virtualization disabled;
- realizes every row through the ordinary renderer.

## Wrap items into rows or columns

Use the ordinary retained-control renderer for a card, tile, or tag surface:

```xml
<ItemsControl Name="Cards"
              ItemsSource="{Binding Cards}"
              Orientation="Vertical"
              Wrap="true"
              Spacing="12"
              JustifyContent="SpaceBetween"
              AlignItems="Stretch">
  <ItemsControl.ItemTemplate>
    <Border Width="180" FlexGrow="1" Padding="10">
      <Label Text="{Binding Title}" AutoSize="true" />
    </Border>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`Orientation` remains the scrolling axis. Vertical orientation flows items
across rows and adds rows downward, so overflow scrolls vertically. Horizontal
orientation flows items down columns and adds columns forward, so overflow
scrolls horizontally. `AutoScroll` is already true by default.

`Spacing` supplies both the gap between items in one line and the gap between
wrapped lines. `JustifyContent` accepts `Start`, `Center`, `End`,
`SpaceBetween`, or `SpaceAround`. `AlignItems` accepts `Start`, `Center`, `End`,
or `Stretch`; stretching respects an explicit item-root cross-axis size. A
non-negative `FlexGrow` on the item-template root shares remaining space in
that line. These attributes accept `Binding`, `Function`, and `Preset`
expressions as well as their literal values.

Effective `FlowDirection`/`RightToLeft` mirrors physical row or column
progression while collection indexes and item identity remain logical. Resize
reflow updates the retained native controls' bounds; unchanged templates are
not recreated, detached, or disposed.

Wrapping and viewport virtualization use different geometry models. The
runtime therefore rejects `Wrap="true"` together with `Virtualizing="true"`
regardless of attribute or property assignment order. It does not silently
disable either feature. Leave virtualization off for a wrapped surface; use a
one-dimensional non-wrapped host when either Controls or Lightweight
virtualization is required.

## Choose smooth or immediate scrolling

`ItemsControl` uses native-style immediate movement by default. Smooth wheel
and scrollbar line/page transitions are opt-in:

```xml
<ItemsControl Name="Results"
              ItemsSource="{Binding Results}"
              SmoothScroll="true"
              SmoothScrollDuration="120">
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`AutoScroll` defaults to `true`, `SmoothScroll` defaults to `false`, and
`SmoothScrollDuration` defaults to `120` milliseconds. The duration
must be greater than zero. All three attributes accept `Binding`, `Function`,
and `Preset` expressions as well as literal values. Set `SmoothScroll="true"`
only when an interpolated transition is desired.

Repeated wheel, arrow, or page input updates the active target instead of
queuing independent animations. Retargeting preserves the transition's current
fractional position and velocity, so a burst or direction reversal does not
restart a new easing curve. Precision-wheel deltas are converted
proportionally; they do not wait for a complete 120-unit wheel notch. This
applies to the native scrollbar and the optional framework-owned scrollbar
described below. Thumb tracking cancels an active transition instead of queuing
another animation. The same behavior is used by ordinary,
Controls-virtualized, and Lightweight item hosts without rebuilding rows merely
to publish an animation frame.

When focus is inside an item, navigation keys that the focused child does not
consume scroll the nearest `ItemsControl`. This includes the configured-axis
arrow keys, Page Up/Down, Home, and End. An editor keeps its caret/navigation
keys, and an inner `ItemsControl` gets the first opportunity, so nested item
hosts do not double-scroll. Mouse-wheel input follows normal WinForms bubbling
with the same nearest-host behavior.

## Style the active scrollbar

`VerticalScrollStyle` and `HorizontalScrollStyle` are nullable
`ScrollBarStyle` properties on `ItemsControl`. Omit both properties for the
native WinForms scrollbar. Assign a non-null style only when the application
needs control over the track, thumb, arrows, border, or thickness:

```xml
<Form Name="StyledResultsForm" Text="Styled results">
  <Presets Name="Theme" Selected="Dark" Default="Light">
    <Preset Name="Light">
      <Set Key="ScrollTrack" Value="#F1F5F9" />
      <Set Key="ScrollThumb" Value="#94A3B8" />
      <Set Key="ScrollThumbHover" Value="#64748B" />
      <Set Key="ScrollThumbPressed" Value="#475569" />
      <Set Key="ScrollArrow" Value="#334155" />
      <Set Key="ScrollArrowHover" Value="#0F172A" />
      <Set Key="ScrollBorder" Value="#CBD5E1" />
      <Set Key="ScrollThickness" Value="14" />
      <Set Key="ScrollGap" Value="8" />
    </Preset>
    <Preset Name="Dark">
      <Set Key="ScrollTrack" Value="#171A1F" />
      <Set Key="ScrollThumb" Value="#596270" />
      <Set Key="ScrollThumbHover" Value="#737E8E" />
      <Set Key="ScrollThumbPressed" Value="#8E99A8" />
      <Set Key="ScrollArrow" Value="#D5DAE1" />
      <Set Key="ScrollArrowHover" Value="#FFFFFF" />
      <Set Key="ScrollBorder" Value="#303640" />
      <Set Key="ScrollThickness" Value="14" />
      <Set Key="ScrollGap" Value="8" />
    </Preset>
  </Presets>

  <ItemsControl Name="Results"
                ItemsSource="{Binding Results}"
                Orientation="Vertical"
                AutoScroll="true"
                SmoothScroll="true"
                SmoothScrollDuration="120"
                ScrollBarGap="{Preset Theme.ScrollGap}"
                KeepScrollBarOnRight="true">
    <ItemsControl.VerticalScrollStyle>
      <ScrollBarStyle TrackColor="{Preset Theme.ScrollTrack}"
                      ThumbColor="{Preset Theme.ScrollThumb}"
                      ThumbHoverColor="{Preset Theme.ScrollThumbHover}"
                      ThumbPressedColor="{Preset Theme.ScrollThumbPressed}"
                      ArrowColor="{Preset Theme.ScrollArrow}"
                      ArrowHoverColor="{Preset Theme.ScrollArrowHover}"
                      BorderColor="{Preset Theme.ScrollBorder}"
                      Thickness="{Preset Theme.ScrollThickness}"
                      MinimumThumbLength="10" />
    </ItemsControl.VerticalScrollStyle>

    <ItemsControl.ItemTemplate>
      <Label Text="{Binding Title}" Padding="8" />
    </ItemsControl.ItemTemplate>
  </ItemsControl>
</Form>
```

Each `ScrollBarStyle` color and metric accepts a literal, Binding, Function, or
Preset expression. A horizontal item host uses the matching property element:

```xml
<ItemsControl Orientation="Horizontal"
              ItemsSource="{Binding Cards}">
  <ItemsControl.HorizontalScrollStyle>
    <ScrollBarStyle TrackColor="#171A1F"
                    ThumbColor="{Preset Theme.ScrollThumb}"
                    Thickness="14" />
  </ItemsControl.HorizontalScrollStyle>
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" Width="160" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`ScrollBarGap` belongs to `ItemsControl`, not `ScrollBarStyle`. It reserves the
same host-owned space whether the active renderer is native or custom. It also
follows a left-side vertical bar in RTL and reserves space above a horizontal
bar. Changing it re-lays out retained rows without recreating them.

For a style object created in C#, use an object-valued expression attribute
instead. All three dynamic expression sources are accepted:

```xml
<ItemsControl VerticalScrollStyle="{Binding ResultsScrollStyle}" />
<ItemsControl VerticalScrollStyle="{Function CreateResultsScrollStyle()}" />
<ItemsControl VerticalScrollStyle="{Preset RuntimeStyles.ResultsScrollStyle}" />
```

The expression must return an actual `ScrollBarStyle` object or null; an XML
string is not a `ScrollBarStyle` literal. The nested property-element form is the
inline object syntax. Omitted, empty, `false`, null, and unresolved dynamic
values all keep the native scrollbar. This permits an optional preset or binding
to enable styling without needing a placeholder style object.

The inline object can also be conditional. Put `Condition` on the property
element so false restores the underlying `null` style and native chrome:

```xml
<ItemsControl.VerticalScrollStyle
    Condition="{Binding UseCustomScrollBar}">
  <ScrollBarStyle TrackColor="#171A1F"
                  ThumbColor="#596273"
                  Thickness="14" />
</ItemsControl.VerticalScrollStyle>
```

The style object is retained while inactive, so a later true value reuses its
configuration. The host, items, focus, logical offset, and virtualization state
are preserved across the native/custom transition. An unresolved condition is
inactive. Direct `<ScrollBarStyle Condition="...">` syntax is not supported;
the condition belongs to `ItemsControl.VerticalScrollStyle` or
`ItemsControl.HorizontalScrollStyle`.

Only one axis is ever active. `Orientation="Vertical"` uses
`VerticalScrollStyle`; `Orientation="Horizontal"` uses
`HorizontalScrollStyle`. It is valid to configure both when `Orientation` is
bound and can change. The inactive style value is retained, but no second
scrollbar is created or displayed. `AutoScroll` still decides whether scrolling
is available.

The style changes only the scrollbar presentation; it does not select a row
renderer:

| ItemsControl path | Styled scrollbar behavior |
| --- | --- |
| Ordinary, `Virtualizing="false"` | Works with the normal fully realized native-control tree. |
| `VirtualizationMode="Controls"` | Uses the same logical offset and synchronous realized-range publication as the native scrollbar. |
| `VirtualizationMode="Lightweight"` | Uses the same owner-drawn row surface and strict template profile; adding a style does not relax or change that profile. |

With the default `SmoothScroll="false"`, wheel, arrow, and page commands publish
the target by moving the live control tree directly; themed children are never
replaced by a bitmap. With `SmoothScroll="true"`, those commands retarget one
coalesced transition. An eligible fully realized list can use the bounded
bitmap transaction for its intermediate frames, while virtualized modes
continue to update only their visible range.
Thumb behavior is controlled separately by `LiveScroll`: when true, dragging
updates content directly; when false, the framework thumb moves during the drag
and commits the content offset when released.

Direction affects geometry, not logical values. A vertical framework scrollbar
is on the right when `KeepScrollBarOnRight="true"`; otherwise it follows the
effective content direction. In horizontal RTL, logical offset zero is the
right-hand start and increasing logical offsets move forward through the items.
Range and direction changes preserve that logical offset, clamping it only when
the new range is shorter. Four-part padding in row content remains in physical
`left,top,right,bottom` order.

A non-null style is an explicit choice of framework-owned chrome, not an
automatic platform fallback. Omitted/null means the existing native scrollbar,
including its operating-system appearance and behavior, with zero
framework-chrome overhead. On the styled path, `ScrollableControl` can restore
the active native axis while moving child windows as well as during range/layout
reconciliation. Every styled frame verifies the actual window style and
re-hides the native axis only if it returned, before the frame paints. This
avoids native/custom flashing without item measurement, ordinary row layout,
control recreation, or a list rebuild.

The framework bar is viewport chrome implemented as a sibling HWND outside
`ItemsControl.Controls`. Scrolling content therefore cannot translate or resize
its track or arrow endpoints, and there is no snap-back correction step. The
inactive native axis is also suppressed before its transient layout state can
reduce the client area. This applies to the fully realized
`Virtualizing="false"` path as well as both virtualization backends, including
RTL layouts and complex item trees.

Variable Controls virtualization measures rows as they enter the viewport. The
framework scrollbar uses only the layout-owned content extent and viewport; it
never substitutes transient native scrollbar state. Each visual frame publishes
the content origin and thumb position together, with no delayed range-commit
timer. Measurement preserves the visible item and its intra-item offset while
an estimate is replaced. Set
`EstimatedItemSize` near the expected row size to reduce correction work, but
correctness no longer depends on an exact estimate.

With `Virtualizing="true"`, the selected Controls backend activates when the
item count reaches `VirtualizationThreshold`, realizes the viewport plus a
small overscan area synchronously, retains a bounded same-item reuse cache, and
adapts to variable item sizes as rows are measured.

Eligible templates also receive a compiled construction blueprint. The runtime
resolves the complete parameterless Control tree once, then constructs rows from
that immutable plan without cloning or walking XML again. Eligible native CLR
attributes use pre-resolved writable properties or events; shareable immutable
static constants are converted once, and every child edge records its layout,
TabPage, item-collection, or ordinary Controls attachment strategy. Bindings,
functions, presets, two-way slots, static events, and names retain their normal
behavior; there is no separate authoring syntax.

The optimization is all-or-nothing. A template containing a registered
component, constructor-selected `Object`/`Control`, property element, inline
preset declaration, nested template, condition, attached property, dynamic
event, mapped XAML alias, applicable implicit or named resource style,
non-shareable static constant, or non-Control child selects the general renderer
before any row object is created. This is a performance choice, not a feature
restriction: the same markup remains supported by the authoritative XML path,
including its mapping and style-precedence behavior. Registering a new global
component also invalidates an older blueprint before its next use.

The virtual path is deliberately direct and synchronous. When a scroll, resize,
or item refresh requires viewport reconciliation, it computes a logical-index
range and realizes it on the UI thread in that operation. Variable-size rows are
measured there as well. If an estimate changes the requested range, each direct
layout pass performs a small bounded number of immediate corrections; the final
native layout pass continues convergence when necessary. The bound prevents a
custom control whose preferred size alternates with scrollbar geometry from
trapping the UI thread. The viewport engine does not schedule timers or
`BeginInvoke` work. Entering controls are built and hidden before the new sorted
range is published. If construction fails or a reentrant refresh supersedes it,
newly staged ownership is cleaned up and the last committed range stays
authoritative. Only after a successful publication are leaving rows cached or
disposed. `ProgressiveRendering` continues to control the ordinary nonvirtual
renderer; it does not defer direct viewport realization.

Direct virtualization requires `AutoScroll="true"` and a stable template root
where one logical item always contributes one root control. A `Condition` on
the item-template root or on a component root can remove that item, so such a
template automatically uses the normal keyed renderer. A root
`Visibility="Collapsed"` or dynamic root `Visibility` uses the same fallback;
`Visibility="Hidden"` retains its layout slot and remains eligible. The runtime
also verifies configured roots before publishing a direct range, so an implicit,
named, or dynamically selected style that actually collapses a row transfers
the refresh to the keyed renderer without exposing a partial range. Conditions
and visibility below the stable root do not change row membership and remain
eligible.

Design note: this separation is inspired by [Mono's `ListView`
implementation](https://github.com/mono/mono/blob/0f53e9e151d92944cacab3e24ac359410c606df6/mcs/class/System.Windows.Forms/System.Windows.Forms/ListView.cs)
and its broad model of a logical `VirtualListSize`, visible-range retrieval,
and a cache used only as a hint. WinFormsXaml's implementation is independent
and narrower: it realizes XML templates synchronously and applies its own
binding, generation, and control-ownership rules. It is not a port and does not
claim Mono `ListView` feature parity.

The shared correctness invariants are explicit:

| Invariant | Mono ListView | WinFormsXaml ItemsControl |
| --- | --- | --- |
| Activation | `VirtualMode` defaults to false | `Virtualizing` defaults to false |
| Logical source | `VirtualListSize` is independent of a realized item collection | `ItemsSource` and the viewport model remain independent of realized row controls |
| Fixed-row projection | The first visible index is derived synchronously from the scroll marker and item size | A fixed-size viewport derives the visible half-open index range directly from the logical scroll offset and stride |
| Retrieval/publication | A requested display index is retrieved when needed | Every visible index is constructed or reused and published before the scroll operation returns |

WinFormsXaml additionally supports arbitrary XML control trees and measured
variable-height rows. Those features cannot use Mono's fixed-position formula
unchanged, so the mutable extent index is tested with the same stronger outcome:
after every jump, reversal, speed change, resize, or end clamp, the committed
controls must cover the complete client viewport with contiguous logical rows.

Start with the simple markup and add tuning only after measuring a real list.

## Add a stable key

Declare the identity member when items can move, be inserted, or be removed:

```xml
<ItemsControl Name="Results" ItemKeyPath="Id">
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

Keys must be stable and unique within the source. Nested paths are supported:

```xml
<ItemsControl Name="Results" ItemKeyPath="Identity.Value">
```

Without a usable key, index is the fallback identity. That is fine for small,
append-only lists but causes more work when rows reorder.

## Add an inexpensive version value

For frequently reloaded data, a version lets the runtime skip ordinary binding
evaluation when an item is unchanged:

```csharp
public sealed class SearchResult
{
    public int Id;
    public int Version;
    public string Title;
    public string Description;

    public void Rename(string title)
    {
        Title = title;
        Version++;
    }
}
```

```xml
<ItemsControl Name="Results"
              ItemKeyPath="Id"
              ItemVersionPath="Version">
  <ItemsControl.ItemTemplate>
    <StackPanel>
      <Label Text="{Binding Title}" />
      <Label Text="{Binding Description}" />
    </StackPanel>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

Increment `Version` whenever non-observable item state used by the template
changes. Treat it as a promise: if the version stays unchanged, the runtime is
allowed to retain ordinary output. Tracked observable root/component
`Condition` dependencies and preset selection changes invalidate keyed output
independently, and `ForceReloadItems` reevaluates even an untracked root
condition. Because a root condition can remove a logical row, that template
uses the normal keyed renderer instead of the direct virtual path. A replacement
item instance rebuilds its dependency graph even when its version compares
equal; a later observable signal also invalidates the fast path.

## Use fixed item size for direct virtualization

For active direct Controls virtualization, a fixed size avoids variable-extent
measurement. Lightweight also requires a positive fixed row height:

```xml
<ItemsControl Name="Results"
              ItemKeyPath="Id"
              ItemVersionPath="Version"
              Virtualizing="true"
              VirtualizationThreshold="64"
              FixedItemSize="64"
              Spacing="4">
  <ItemsControl.ItemTemplate>
    <Panel Height="64">
      <!-- row content -->
    </Panel>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

Once direct virtualization is active, `FixedItemSize` is row height for a
vertical list and row width for a horizontal Controls list. A Controls profile
below `VirtualizationThreshold` is still using the ordinary renderer: the
setting does not resize those rows, and each template root keeps its desired
size or explicit `Height`/`Width`.

Keep `FixedItemSize` at `0` when active Controls rows genuinely vary, and give
that direct viewport a useful starting estimate:

```xml
<ItemsControl Name="Messages"
              Virtualizing="true"
              EstimatedItemSize="88">
```

`EstimatedItemSize` is used only before active direct Controls virtualization
has measured a variable-size item. It does not size ordinary nonvirtual rows and
is not the row-height source for Lightweight.

## Tune progressive rendering

The defaults prioritize a responsive UI. Progressive rows are constructed in
bounded detached batches while the previous complete item tree remains visible.
The new tree is published atomically after construction, so users never see one
early row followed by a delayed or blank viewport. A larger batch can finish a
cheap template sooner on a fast machine:

```xml
<ItemsControl Name="Results"
              ProgressiveRendering="true"
              ProgressiveBatchSize="16"
              ProgressiveTimeBudgetMs="6" />
```

For a tiny list whose result must be complete before `SetItems` returns:

```xml
<ItemsControl Name="ShortList"
              ProgressiveRendering="false"
              Virtualizing="false" />
```

Keep long work, file access, and network access outside item getters and
functions. Prepare the data first; let the UI-thread refresh only create and
update controls.

## Tune virtualization only after measuring

```xml
<ItemsControl Name="Results"
              Virtualizing="true"
              VirtualizationThreshold="64"
              OverscanItems="4"
              EstimatedItemSize="48"
              VirtualizationCacheItems="24" />
```

- Omit `Virtualizing`, or set it to `false`, to keep the normal renderer
  regardless of count. This is the default.
- Raise `VirtualizationThreshold` when medium lists are faster fully realized.
  Until a Controls list reaches the threshold it remains on the ordinary
  renderer, even with `Virtualizing="true"`; `FixedItemSize` and
  `EstimatedItemSize` do not replace its root controls' desired or explicit
  main-axis geometry.
- Increase `OverscanItems` if fast scrolling exposes rows too late. It defines
  a fixed `2*N` extra-row budget. The initial Controls viewport splits it
  evenly; scrolling biases the same total ahead of travel, and duplicate
  callbacks at the settled offset retain that published bias.
- Reduce it when control creation or native handle count is the limiting factor.
- Set `EstimatedItemSize` close to the real main-axis row size when active direct
  Controls rows vary.
- Increase `VirtualizationCacheItems` when revisiting nearby rows is common.
- Reduce it, or set it to `0`, on low-resource systems. The cache is only a
  reuse hint and never determines logical correctness. It is same-item only
  unless the explicit Controls recycling contract below is enabled.
- Prefer `FixedItemSize` when every active direct row has one exact main-axis
  size; leave it at `0` for variable Controls rows and tune `EstimatedItemSize`
  instead. Lightweight always requires a positive value.

Inspect the live state when diagnosing a list:

```csharp
int itemCount = Ui.GetItemsControl("Results").Count;
int realized = Ui.GetItemsControl("Results").RealizedCount;
int cached = Ui.GetItemsControl("Results").VirtualCacheCount;
bool virtualized = Ui.GetItemsControl("Results").IsVirtualizing;

long created = Ui.GetItemsControl("Results").VirtualCreatedCount;
long retained = Ui.GetItemsControl("Results").VirtualRetainedReuseCount;
long reusedFromCache = Ui.GetItemsControl("Results").VirtualCacheReuseCount;
long recycledAcrossItems =
    Ui.GetItemsControl("Results").VirtualCrossItemRecycleCount;
long rejectedRecycles =
    Ui.GetItemsControl("Results").VirtualCrossItemRecycleRejectedCount;
long compiledRows =
    Ui.GetItemsControl("Results").ItemTemplateBlueprintBuildCount;
long completeRendererRows =
    Ui.GetItemsControl("Results").ItemTemplateFallbackBuildCount;
long disposedRows =
    Ui.GetItemsControl("Results").ItemControlTreeDisposedCount;
int activeItemSubscriptions =
    Ui.GetItemsControl("Results").ActiveItemBindingSubscriptionCount;
```

These counters are monotonic for the lifetime of one ItemsControl and do not
allocate during scrolling. Compare deltas around a scenario instead of treating
their absolute values as a benchmark. `ProgressiveBatchCount` measures timer
batches in the normal progressive renderer; direct virtualization is
synchronous and does not increment it. The two item-template counters classify
newly constructed control trees; retained, cached, recycled, and Lightweight
rows are accounted for by their own paths rather than counted as constructions.

The Controls viewport stages a complete entering range before replacing the
published records. Variable-height correction retains every range visited by
the current synchronous pass. Near the source end, unused forward visible
capacity is shifted backward before measurement, so a large native clamp does
not expose an empty client area one short row at a time.
The active-subscription count covers prepared Controls and Lightweight row
bindings; compare it before and after scrolling or replacement to catch
subscription growth that the stable realized-row count would otherwise hide.

## Opt in to cross-item Control recycling only with a reset contract

The default `ItemRecycling="Disabled"` policy is conservative: a detached
cached tree is reused only when the same item instance and stable key returns.
Setting `ItemRecycling="Explicit"` adds a second lookup after that exact match.
It never assumes that an arbitrary control tree is safe.

```xml
<ItemsControl Name="Notifications"
              ItemsSource="{Binding Notifications}"
              Virtualizing="true"
              VirtualizationMode="Controls"
              ItemRecycling="Explicit"
              VirtualizationCacheItems="24">
  <ItemsControl.ItemTemplate>
    <NotificationRowControl>
      <Label Text="{Binding Title}" />
      <CheckBox Checked="{Binding Enabled, Mode=TwoWay}" />
    </NotificationRowControl>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

The item-template root must implement `IRecyclableItemControl`:

```csharp
public sealed class NotificationRowControl : Panel,
    IRecyclableItemControl
{
    public bool TryPrepareForRecycle(ItemRecycleContext context)
    {
        // Reset only transient state not owned by XAML values.
        StopAnimation();
        ClearHoverAndEditState();

        // Return false when this particular tree cannot safely change item.
        return true;
    }
}
```

The callback receives immutable `ItemsControl`, `Control`, `OldItem`, `NewItem`,
`OldIndex`, and `NewIndex` values. It runs on the UI thread while the cached root
is detached and all old item-binding subscriptions are inactive. It may reset
selection, hover, expansion, editing, or animation state. It must not dispose,
reparent, or structurally change the row tree, and it should not change static
XAML-owned properties.

After an accepted reset, the runtime forces every dynamic Binding, Function,
and Preset slot to the new value, updates the tree data context and logical
index, reactivates subscriptions (including TwoWay targets), reapplies inherited
properties/layout, and publishes the tree transactionally. Event handlers,
names, control ownership, and the static template shape retain their original
identity.

Returning `false` is a normal per-item decline: that cached record is disposed
once, is never returned to a cache or pool, and a fresh tree is constructed.
Throwing indicates a broken reset and fails the refresh visibly while the
previously committed range remains authoritative. A dynamic slot that has no
live target or requires structural reconstruction is rejected before the
callback and also uses fresh construction.

This path requires the native `Controls` backend, active direct virtualization,
`ReuseItems="true"`, and a nonzero `VirtualizationCacheItems`. It has no effect
in `Lightweight` mode. `VirtualCacheReuseCount` includes every successfully
published detached cached-tree reuse; `VirtualCrossItemRecycleCount` is its
precise cross-item subset, and `VirtualCrossItemRecycleRejectedCount` counts
clean declines and structural-slot rejections.

## Use the explicit lightweight renderer for paint-only rows

When virtualization is enabled, `VirtualizationMode="Controls"` is the default
backend. It creates a normal WinForms control tree for each visible and
overscanned row and supports the complete item-template vocabulary.

Choose `Lightweight` only when the row fits its deliberately small owner-drawn
profile:

```xml
<ItemsControl Name="Notifications"
              ItemsSource="{Binding Notifications}"
              VirtualizationMode="Lightweight"
              Virtualizing="true"
              AutoScroll="true"
              Orientation="Vertical"
              FixedItemSize="52"
              Spacing="2">
  <ItemsControl.ItemTemplate>
    <Border Padding="8" Background="#FFFFFF"
            BorderBrush="#D0D0D0" BorderThickness="1">
      <StackPanel Orientation="Horizontal" Spacing="8">
        <Image Width="32" Source="{Binding IconBytes}" Stretch="Uniform" />
        <CheckBox Width="28"
                  Checked="{Binding Enabled, Mode=TwoWay}" />
        <Label Text="{Binding Title}" AutoEllipsis="true" />
        <HyperlinkLabel Width="72" Text="Open"
                        NavigateUri="{Binding Url}" />
      </StackPanel>
    </Border>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

This backend paints only the visible fixed-height rows on the `ItemsControl`
itself. It does not create a `Control`, HWND, layout tree, or separate binding
registration for each dynamic property in a row. A cached row whose values
have observable dependencies owns at most one aggregate source-only
registration. The bounded
row-value cache prepares the visible rows plus a fixed `2 * OverscanItems`
budget: initial/stationary viewports split it symmetrically, while a known
scroll direction shifts the same budget ahead. Painting remains visible-only;
`VirtualCacheCount` therefore remains `0` and `RealizedCount` means currently
visible painted rows.

The first profile supports:

- a vertical, scrolling `ItemsControl` with a positive `FixedItemSize`;
- a row root of `Border`, `StackPanel`, `Label`, `CheckBox`,
  `HyperlinkLabel`, or `Image`;
- one `Border` around one leaf or one `StackPanel`;
- a `StackPanel` containing `Label`, `CheckBox`, `HyperlinkLabel`, and `Image`
  leaves;
- static margin, padding, size, font, alignment, orientation, spacing, and
  border geometry;
- dynamic text, colors, enabled/check state, and hyperlink destinations via
  Binding, Function, or Preset expressions;
- checkbox hit testing when `Checked`/`IsChecked` is one complete
  `Mode=TwoWay` binding, and hyperlink activation through the default system
  application;
- `Image` with static `Stretch=None|Fill|Uniform|UniformToFill` and a dynamic
  `Source` returning `System.Drawing.Image`, `Icon`, or encoded `byte[]`.

The lightweight image path normally draws the original image directly.
Downscaled, non-animated `Icon` and encoded `byte[]` conversions may also use a
small per-`ItemsControl` thumbnail cache so repeated paints do not resample the
same large runtime-owned bitmap. The cache holds at most 16 thumbnails, rejects
outputs above 65,536 pixels, and is cleared on logical refresh, source
replacement, backend deactivation, and disposal. Application-provided `Image`
objects, transformed paint surfaces, upscaling, and animated images never use
the thumbnail path, so in-place caller mutations remain visible and caller
ownership is unchanged. Runtime conversions continue through the shared
bounded decoded-image cache and reference-counted ownership. A plain URI/file
string and an animated image require `VirtualizationMode="Controls"`, which
provides PictureBox loading and ImageAnimator lifecycle behavior.

It intentionally does not yet support editors, buttons, PictureBox/URI loading,
animated images, nested layout, components, styles/resources, conditions,
control events, variable row height, or horizontal lists. A disabled read-only
checkbox is allowed. An enabled
checkbox must use `Mode=TwoWay`, because an owner-painted row has no hidden
per-control state in which to retain an unbound edit.

These restrictions are checked when the template is loaded. Unsupported
lightweight markup throws `WinFormsXamlLoadException` (an
`InvalidOperationException`) with the source, element path, property, line, and
position. Final host eligibility is checked after all attributes and the
`ItemTemplate`, even when `ItemsSource` is null, so attribute order cannot hide
an invalid request. Later changes to Lightweight eligibility/layout settings
either refresh successfully or restore the previous setting. The runtime never
converts an invalid lightweight request into a `Controls` row tree. Use
`VirtualizationMode="Controls"` explicitly when the template needs anything
outside this profile.

`ItemsSource` replacement, `IBindingList`/`ItemsBinding` changes,
`ReloadItems`, `ForceReloadItems`, preset reloads, `ClearItems`, and
`ScrollToIndex` use the same public APIs in both modes. `PropertyBinding` and
`INotifyPropertyChanged` changes used by any dynamic lightweight slot rebuild,
re-resolve nested dependencies for, and invalidate only the affected cached
row. Explicit observable path arguments in `Function` expressions participate
as well; automatic whole-context Function calls, plain fields, and external
state still require `ReloadItems`. Row retirement detaches its aggregate
registration. TwoWay checkbox writes retain their source semantics.

Visited hyperlink state is destination-aware and capped at 256 entries per
host with deterministic oldest-first eviction. It is released when the backend
is deactivated or disposed, and a template replacement clears incompatible
keys. Disposing the `ItemsControl` also clears row/image caches, fonts, and
runtime references.

## Force a full rebuild only when required

Normal item bindings are detected and patched by `ReloadItems`. Use
`ForceReloadItems` when output depends on external state that is not represented
by an item, its version, a preset, or a tracked function result:

```csharp
_thumbnailProvider.ClearCache();
Ui.ForceReloadItems("Results");
```

This intentionally discards realized and cached template trees, so it is more
expensive than a normal reload. It also reevaluates root/component membership
in the normal keyed renderer even when `ItemVersionPath` has not changed.

## Scroll from C#

```csharp
ItemsControl results = Ui.GetItemsControl("Results");

results.ScrollToStart();
results.ScrollToIndex(2500); // retained immediate leading-edge shortcut

results.ScrollIntoView(2500); // Nearest; follows results.SmoothScroll
results.ScrollIntoView(2500, ItemScrollAlignment.Center);
results.ScrollIntoView(2500, ItemScrollAlignment.End, false);
results.ScrollIntoView(2500, ItemScrollAlignment.Start, true);
```

The alignments are relative to the active scrolling axis:

- `Nearest` leaves a fully visible item in place and otherwise performs the
  smallest movement that reveals it.
- `Start` places it at the logical leading edge.
- `Center` centers it in the available item viewport.
- `End` places it at the logical trailing edge.

For a horizontal RTL host, `Start` is the logical right edge and `End` is the
logical left edge. Application code does not need to translate to native
physical scrollbar coordinates. Framework-owned scrollbar thickness and host
padding are excluded from the available viewport calculation.

The overload without an `animate` argument follows `SmoothScroll`. Passing
`true` explicitly animates even when `SmoothScroll` is false; passing `false`
is immediate even when it is true. One in-flight animation is retargeted rather
than duplicated. Immediate Controls virtualization realizes the requested
range before returning. Lightweight uses the same alignment contract and keeps
its documented fixed-height vertical restriction.

Call item APIs on the WinForms UI thread. After the control handle exists,
cross-thread calls are queued with `BeginInvoke` and return before the queued
operation completes; before a handle exists, there is no safe WinForms target
for that marshal and the call fails clearly.
