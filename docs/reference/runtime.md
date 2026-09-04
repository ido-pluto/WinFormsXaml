# Runtime API

A `XamlRuntime` instance owns the metadata and resources for one loaded
XML object tree.

## Load an embedded interface

```csharp
using WinFormsXaml;

XamlRuntime ui = XamlRuntime.LoadEmbedded(
    "MyProduct.UI.MainForm.xml",
    this);
```

The `eventTarget` argument supplies binding state, function methods, and event
handlers. The resource is resolved from that object's assembly. Relative file
paths in embedded markup resolve from `Application.StartupPath`, so they do not
depend on the process's current working directory.

Open the application's `.csproj` file as text and manually add the XML resources
inside the root `<Project>` element:

```xml
<ItemGroup>
  <EmbeddedResource Include="UI\*.xml" />
</ItemGroup>
```

WinFormsXaml does not add application XML files automatically. In Visual
Studio, setting each XML file's **Build Action** to **Embedded Resource**
produces the same project entry.

Available overloads:

```csharp
XamlRuntime.LoadEmbedded(string resourceName, object eventTarget)
XamlRuntime.LoadEmbedded(Assembly assembly, string resourceName,
                          object eventTarget)
```

## Load an XML string

```csharp
XamlRuntime.Load(string xml)
XamlRuntime.Load(string xml, object eventTarget)
XamlRuntime.Load(string xml, object eventTarget, string basePath)
```

`basePath` resolves relative file paths.

```csharp
XamlRuntime ui = XamlRuntime.Load(
    xml,
    this,
    Application.StartupPath);
```

## Inspect load failures

Parsing and tree-construction failures use the serializable
`WinFormsXamlLoadException`, which retains structured context in addition to
its complete `Message` and `InnerException`:

| Member | Meaning |
| --- | --- |
| `MarkupSource` | Embedded-resource name, registered-component resource, or `inline XML`. |
| `ElementPath` | Deepest known path, such as `/Form#MainForm/Panel/Button#Save`, or `null` when parsing failed first. |
| `PropertyName` | Property or attribute being applied, or `null` when unknown. |
| `LineNumber` | One-based parser or semantic source line, or `0` when no source location is available. |
| `LinePosition` | One-based parser or semantic source position, or `0` when no source location is available. |

```csharp
try
{
    using (XamlRuntime ui = XamlRuntime.Load(xml, this))
    {
        Application.Run(ui.Form);
    }
}
catch (WinFormsXamlLoadException ex)
{
    System.Diagnostics.Trace.WriteLine(ex.Message);
    System.Diagnostics.Trace.WriteLine(ex.MarkupSource);
    System.Diagnostics.Trace.WriteLine(ex.ElementPath);
    System.Diagnostics.Trace.WriteLine(ex.PropertyName);
    System.Diagnostics.Trace.WriteLine(
        ex.LineNumber.ToString() + ":" +
        ex.LinePosition.ToString());
}
```

Malformed XML comes from the XML parser and carries `LineNumber` and
`LinePosition`; its element and property may be unknown, and parser behavior is
unchanged. Semantic property failures carry the markup source, deepest element
path, and property name. Their location identifies the exact failing attribute
when it is retained, otherwise the deepest opening element. These are original
source coordinates even when an item template is cloned or a registered
component is serialized through `TemplateXml` and parsed again. A registered
XML component reports its own resource as the source while its path includes
both the invocation and the failing template element.

## Root and named objects

| Member | Result |
| --- | --- |
| `Root` | Root XML object as `object`. |
| `RootControl` | Root as `Control`, or `null` when it is not a control. |
| `Form` | Required native root `Form`; throws when the root is not a form. |
| `IsDisposed` | Whether disposal completed successfully. |
| `NamedObjects` | Case-insensitive name-to-object dictionary. |
| `Names` | Names registered in the global object tree. |
| `this[string name]` | Required named-object lookup as `object`. |
| `Get<T>(name)` | Required named-object lookup as `T`. |
| `GetControl(name)` | Required named-object lookup as `Control`. |
| `Contains(name)` | Whether a global name exists. |

Use the native WinForms type declared in XML:

```xml
<Form Name="MainForm">
  <TextBox Name="SearchText" />
</Form>
```

```csharp
Form form = ui.Form;
TextBox search = ui.Get<TextBox>("SearchText");
```

`Get<T>` throws when the name is absent or the object is not assignable to `T`.
Names inside repeated item templates are local to each template instance and do
not appear in the global map.

Use `ui.Form` for the root. `Get<T>(name)` remains for named child objects that
need imperative access.

## XmlForm code-behind sugar

Derive from `XmlForm` when one C# class owns one embedded form:

```csharp
using System;
using WinFormsXaml;

namespace MyProduct.UI
{
    public sealed class MainForm : XmlForm
    {
        public readonly PropertyBinding<string> Title =
            new PropertyBinding<string>("Ready");

        public MainForm()
            : base("MainForm.xml")
        {
        }
    }
}
```

`XmlForm.WinForm` returns the native form, `Ui` exposes the protected runtime,
and `Get<T>(name)` provides protected named-child lookup. Code-behind can use
the protected `Presets.Select(...)` shortcut and can call `ReloadBindings()`,
`ReloadBinding(name, property)`, or `UpdateBindingSource(name, property)`
without routing through `Ui`. `Start()` is the
direct shortcut for `Application.Run(WinForm)`.
Loading is lazy until one of these members is requested. A normal
`Start()` after construction therefore binds against fully initialized state.
`WinForm` is also safe to use in the derived constructor body, but that access
loads the XML immediately; initialize binding state first:

```csharp
public readonly PropertyBinding<string> Header;

public MainForm()
    : base("MainForm")
{
    Header = new PropertyBinding<string>("Ready");
    WinForm.Icon = Properties.Resources.customIcon;
}
```

The example uses an explicit partial resource name. If that constructor is
omitted, the parameterless base convention requires the exact
`Derived.Type.FullName.xml` resource name in the derived assembly. Explicit
constructors also accept an exact manifest name or a longer partial path, plus
an assembly when needed. Exact names win; partial matches are ranked
deterministically by suffix, distance, and resource name. Disposal is inherited
and idempotent.

### Queue includes before `XmlForm` loads

Derived forms can queue reusable composition sources through the protected
methods:

```csharp
Include(string source)
Include(string source, IncludeSourceKind sourceKind)
```

The one-argument form selects `IncludeSourceKind.Registered`; the explicit enum
also offers `EmbeddedResource` and `File`. Call every `Include` in the derived
constructor before accessing `WinForm`, `Ui`, or `Get<T>`, because any of those
members starts the lazy XML load. Accessing `Presets` also loads the runtime,
and `Start()` accesses `WinForm`.

```csharp
public MainForm()
    : base("MainForm")
{
    Include("SharedHeader");
    Include("UI/Shared/Diagnostics.xml", IncludeSourceKind.File);
}
```

If the runtime auto-creates the `XmlForm` from an XML `Class` attribute, that
constructor may queue `Include` calls but must not start its own load through
`WinForm`, `Ui`, `Get<T>`, or `Presets`. Directly constructing `new MainForm()`
continues to allow constructor-time `WinForm` access.

See [Reusable includes](/guide/includes) for standalone `<Includes>` documents,
registered/embedded/file directives, nesting, resources, presets, and visual
content.

Use a simple public field for one-way snapshot state and reload it explicitly
after mutation. Use a stable readonly `PropertyBinding<T>` field for reactive or
two-way state and assign through `.Value`. Existing notification-based models
remain supported for compatibility; the two explicit forms are recommended for
new code.

## Automatic XmlForm Name fields

An `XmlForm` can receive named XML objects without `Get<T>` initializers:

```xml
<Label Name="connectionStatus"
       Text="{Binding Title}" />
```

```csharp
private Label connectionStatus = null;

protected override void OnLoaded(EventArgs e)
{
    // Assigned before OnLoaded runs.
    connectionStatus.AutoEllipsis = true;
}
```

Initialize the field explicitly to `null`; this avoids the compiler's
unassigned-field warning and preserves the empty slot required for wiring. It
must be an instance reference field, must be assignable from the named object,
and must not be `readonly`. Private and inherited fields are eligible. An exact
ordinal name wins; otherwise one unambiguous case-insensitive match is accepted.
Wiring finishes before `OnLoaded` and before the first lazy `WinForm` or `Ui`
access returns.
`private Label connectionStatus = Get<Label>("connectionStatus");` is neither
needed nor legal C# instance-field initialization. Use `Get<T>(name)` from
methods or properties when declaration-only wiring is not appropriate, for
example `Get<ProgressBar>("Loading")`.

## XmlForm-owned background threads

`RunThread(XmlFormThreadStart)` is a protected lifetime helper:

```csharp
protected Thread RunThread(XmlFormThreadStart start)
protected bool PostToUi(MethodInvoker callback)
```

It validates the delegate, creates an `IsBackground` thread, starts it
immediately, and returns the started `Thread`. The delegate receives:

| Member | Behavior |
| --- | --- |
| `StopRequested` | `true` after Form shutdown or `XmlForm` disposal requests a cooperative return. |
| `StopWaitHandle` | A wait handle signaled by the same request, for blocking loops without polling. |

```csharp
private void StartWork_Click(object sender, EventArgs e)
{
    RunThread(
        delegate(XmlFormThreadContext context)
        {
            while (!context.StopRequested)
            {
                if (context.StopWaitHandle.WaitOne(250, false))
                    return;

                // Do work; publish results through PropertyBinding<T>.Value.
            }
        });
}
```

`FormClosing` with `CloseReason.UserClosing` is canceled while workers remain,
all stop handles are signaled, and the last returning worker asynchronously
reposts `Close` to the UI thread. Other close reasons are not rewritten as a
different close action. While any close attempt is active, `RunThread` rejects
new owned workers and `PostToUi` rejects new callbacks. If another handler
cancels the close, both become available again after the event and its recovery
message unwind. `XmlForm.Dispose` and direct runtime disposal signal workers and
wait at most two seconds total before beginning their teardown.
Native `Form.Dispose` invokes the same stop/join path synchronously before
runtime and code-behind cleanup. If a worker has not returned, framework cleanup
throws and can be retried after the worker exits. The owner message loop makes
one automatic retry when dispatch is available; if it is not, call `Dispose()`
again explicitly on the owner thread. Threads are never aborted.

Each delegate must return cooperatively and must not retain its context. Update
reactive `PropertyBinding<T>` values directly from the worker. For imperative
control work, `PostToUi` is the `XmlForm` shortcut for an asynchronous Form
dispatch:

```csharp
RunThread(
    delegate(XmlFormThreadContext context)
    {
        string result = LoadStatusText(context);

        PostToUi(
            delegate()
            {
                Get<Label>("Status").Text = result;
            });
    });
```

It returns `true` only when the callback was queued on a live Form handle; this
is queue acceptance, not a guarantee that the callback will run. It returns
`false` before the handle exists and while closing or disposal is active. A
queued callback is suppressed if that lifetime ends before dispatch. If another
`FormClosing` handler cancels the close, posting becomes available again after
the close event and its recovery message unwind. The callback is always
asynchronous. Never use synchronous `Control.Invoke`, because disposal or
closing may be waiting for the worker. An owned delegate also cannot dispose
its own `XmlForm`.

## Form Class and embedded XmlForm discovery

`Class` declares the code-behind CLR type with a static full name:

```xml
<Form Class="MyProduct.UI.MainForm"
      Name="MainForm"
      Text="Main form" />
```

When an `XmlForm` code-behind object is supplied, `Class` verifies its type.
Without a supplied object, direct embedded-runtime loading can create the
declared class when it is concrete and has a public parameterless constructor.
Normal applications create their typed `XmlForm` directly:

```csharp
new MainForm().Start();
```

Use an explicit partial path when the type name and manifest resource name do
not match:

```csharp
public MainForm()
    : base("UI.MainForm")
{
}
```

A complete manifest resource name with exact casing wins before convenience
matching. Otherwise XML resources are matched case-insensitively and ranked by
suffix and distance. Equally ranked matches use deterministic resource-name
order, including differently cased complete-name matches when no exact-cased
name exists. A missing fragment lists up to eight available embedded XML
resources in deterministic name order and reports any remaining count, making
an exact name directly actionable.

## Register components and includes

Registration is global to the current application domain:

```csharp
XamlRuntime.Register()
XamlRuntime.Register(string name, Type componentType)
XamlRuntime.Register(string resourceNameOrFragment)
XamlRuntime.Register(Assembly assembly, string resourceNameOrFragment)
```

```csharp
XamlRuntime.Register();
```

Register components and includes before loading XML that uses them.
Parameterless `Register()` inspects every embedded `.xml` resource in the
calling assembly and retains documents whose root is `Component` or `Includes`.
Passing an empty or whitespace fragment to the assembly overload has the same
scan-all behavior. Well-formed Forms, presets, and unrelated XML are ignored,
so a broad application glob is safe.

Use `Register("UI.Components")` to inspect only resources whose manifest path
contains that fragment, or `Register("ActionButton", typeof(ActionButton))` for
a CLR component. A complete manifest resource name with exact casing registers
that resource strictly and rejects a root other than `Component` or `Includes`.
A differently cased
complete name that matches multiple case-only variants is ambiguous and lists
its candidates. A scan that retains neither components nor includes is a no-op.
The complete batch is registered atomically; malformed XML, a malformed
`Component`, or a malformed `Includes` document rejects it. Each XML component
name comes from its final resource-name segment. The no-argument and one-string
overloads read from the calling assembly.
See [Reusable components](/guide/components) for C# constructors, XML property
declarations, nested forwarding, and reload behavior, and
[Reusable includes](/guide/includes) for XML composition and resolution.

No-match diagnostics list up to eight embedded XML resource candidates in
deterministic name order and report how many additional candidates were
omitted. If a batch contains two resources that derive the same component name,
the error names both resources. If a name is already registered from a different
source, the error describes both the existing and attempted provenance as a CLR
type plus assembly or an embedded XML resource plus assembly. Registering the
same type or exact resource again remains idempotent.

## Binding state sources

Use a public field plus an explicit reload for one-way snapshot state:

```csharp
public string ManualStatus = "Ready";

private void UpdateManualStatus()
{
    ManualStatus = "Connected";
    ReloadBinding("StatusLabel", "Text");
}
```

Use a stable readonly `PropertyBinding<T>` field when state must update markup
automatically, participate in two-way editing, or expose a public
`ValueChanged` event:

```csharp
public readonly PropertyBinding<string> Header =
    new PropertyBinding<string>("Ready");

private void UpdateHeader()
{
    // Preserves the wrapper and every runtime/application subscriber.
    Header.Value = "new";
}
```

| Member | Behavior |
| --- | --- |
| `PropertyBinding()` | Creates a wrapper containing `default(T)`. |
| `PropertyBinding(value)` | Creates a wrapper with an initial value. |
| `Value` | Thread-safe current value; assignment notifies only when unequal. |
| `ValueChanged` | Raised synchronously on the thread that changed `Value`. |

Markup unwraps the wrapper. One-way is the default; `Mode=TwoWay` copies target
edits back into the terminal wrapper:

```xml
<Label Text="{Binding Header}" />
<TextBox Text="{Binding Header, Mode=TwoWay}" />
```

Keep a `PropertyBinding<T>` instance stable so its subscribers remain attached.
`Header = "new"` cannot assign a readonly wrapper field; assign `Header.Value`
instead to preserve the subscribed wrapper identity.
Existing classes that implement `INotifyPropertyChanged` remain supported, with
pooled and member-filtered subscriptions, but the canonical package examples use
snapshot fields or stable wrappers. Runtime source and target notifications are
coalesced and marshalled
to the runtime's WinForms owner thread, through `RootControl` when present;
pre-handle work waits for `HandleCreated`. Reactive non-Control roots use a
private dispatcher without changing their public `RootControl == null`
contract. `PropertyBinding<T>` uses its version token when source and target
edits compete.

The same automatic one-way observation applies to `Condition`. A dynamic false
element is retained as collapsed so a later source change can show it. Because
a condition on an item-template or expanded component root can remove that
row's layout slot, such a template uses the normal keyed renderer instead of
the direct logical-index viewport. Conditions below the stable row root remain
compatible with direct virtualization and are observed while that row is
realized. A complete function expression observes its explicit reactive path
arguments. Snapshot fields and functions without such an argument use the
reload APIs. `Condition` rejects `Mode=TwoWay`.

Two-way validation requires a terminal `PropertyBinding<T>` or an existing
writable notifying property, plus a reversible target property. Fields
that contain plain snapshot values remain one-way. Direct CLR names work. When
the requested name is not itself a target property,
`Content`/`Header`/`Title` map to `Text`, `IsChecked` to `Checked`, `IsEnabled`
to `Enabled`, `IsTabStop` to `TabStop`, `IsReadOnly` to `ReadOnly`,
`Foreground` to `ForeColor`, `Background` to `BackColor`, and
`WebBrowser.Source` to `Url`. Negation,
interpolation, styles, attached properties, `ItemsSource`, and `Condition` are
not two-way targets.
A conventional CLR property uses its descriptor change notification. Built-in
alternate routes cover `SelectedItem` on combo/list controls,
`DomainUpDown.SelectedIndex`, MonthCalendar selection range properties,
`TreeView.SelectedNode`, `TabControl.SelectedTab`, and
writable RichTextBox selection properties. The same bridge covers
`Form.WindowState`, splitter positions, `WebBrowser.Url`,
`ToolStripComboBox.SelectedItem`, `ScrollableControl.AutoScrollPosition`, and
`PropertyGrid.SelectedObject`. Size and location events cover `Width`, `Height`,
`Left`, and `Top`; text events cover `Lines` and `RichTextBox.Rtf`; and
`DataGridView.Scroll` covers its writable displayed-cell/index and horizontal
offset properties. A writable property with neither a descriptor
notification nor one of these reliable native events is rejected instead of
silently degrading to one-way behavior.
A target edit on the runtime owner thread commits the terminal source before a
later WinForms interaction event such as `Click`. Read the current value through
the stable wrapper's `.Value`; sibling source-to-target updates remain queued and
coalesced. Worker-thread changes continue to marshal to the owner dispatcher.
A normalizing CLR setter is re-read so the target reflects its committed value.

Bindings normally start at the current context: the code-behind object in Form
markup, the current item in an item template, or declared local values inside an
XML component. `Source=CodeBehind` explicitly selects the original
`eventTarget`, including from nested templates; `Source=Current` explicitly
preserves the default. Both values are case-insensitive and participate in the
same reactive observation, two-way validation, thread dispatch, virtual-item
reactivation, and disposal cleanup. `CodeBehind` is rejected when no event
target was supplied.

An XML-only component property is represented internally by a typed local
observable value. This allows an inner target such as
`Text="{Binding Value, Mode=TwoWay}"` to edit a literal or default locally. If
the component invocation also uses `Value="{Binding Header, Mode=TwoWay}"`, the
same edit is forwarded to the form's terminal `PropertyBinding<T>`. Disposing
the component root detaches both sides of that route.

An editing value that cannot yet be converted to `T` is not an application
error. The target keeps that value and the strongly typed source remains
unchanged until a later valid edit is entered.

## Reload and commit bindings

| Member | Scope |
| --- | --- |
| `ReloadBindings()` | Every retained global property binding. |
| `ReloadBindings(name)` | The named object and its control subtree. |
| `ReloadBinding(name, property)` | One property on one named object. |
| `UpdateBindingSource(name, property)` | Commit one named TwoWay target, including `UpdateSourceTrigger=Explicit`. |
| `UpdateBindingSource(target, property)` | Commit one TwoWay target object directly. |

```csharp
// Inside an XmlForm code-behind:
ReloadBindings();
ReloadBindings("AccountPanel");
ReloadBinding("Status", "Text");

// The equivalent calls on a directly loaded XamlRuntime are:
ui.ReloadBindings();
ui.ReloadBindings("AccountPanel");
ui.ReloadBinding("Status", "Text");

// Commit a deferred TwoWay editor:
UpdateBindingSource("DraftTitle", "Text");
ui.UpdateBindingSource(ui["DraftTitle"], "Text");
```

If a reactive preset refresh fails after its mutation commits,
`ReloadBindings()` first retries the retained merged preset scope. Persistent
failures are not retried automatically; correct the failing setter, function, or
conversion before calling it. The named and single-property overloads stay
targeted and do not consume that pending retry.

Use these methods for snapshot properties and fields, functions without an
explicit reactive path argument, or an explicit refresh boundary.
`PropertyBinding<T>` values and explicit reactive function path arguments
refresh their dependent properties automatically. Explicit
reload methods directly update WinForms objects and should be called on their
owner thread. A retained binding that fails during an explicit or reactive
refresh throws `WinFormsXamlLoadException`; its structured fields identify the
markup location where that binding was declared.

## Work with repeated items

| Member | Purpose |
| --- | --- |
| `GetItemsControl(name)` | Get the named `ItemsControl`. |
| `SetItems(name, source)` | Assign an `IEnumerable` and render it. |
| `ReloadItems(name)` | Incrementally refresh the existing source. |
| `ForceReloadItems(name)` | Discard reuse state and rebuild every template. |
| `ClearItems(name)` | Clear source data and visuals. |

`ItemsControl.ItemsSource` is a public `IEnumerable` property and can be set in
markup:

```xml
<ItemsControl Name="Results"
              ItemsSource="{Binding Results}">
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

The XML element creates the canonical top-level
`WinFormsXaml.ItemsControl`, so an `XmlForm` can use a declaration-only field
without a nested-type alias:

```csharp
private ItemsControl Results = null;
```

The established `XamlRuntime.ItemsControl` base type remains assignable for
existing consumers, and the canonical top-level type remains extensible for
custom item hosts.

Use `ItemsBinding<T>` for an observable .NET 2.0 list:

```csharp
public readonly ItemsBinding<SearchResult> Results =
    new ItemsBinding<SearchResult>();

Results.Add(new SearchResult("Another result"));
```

`ItemsBinding<T>` derives from `BindingList<T>` and adds
`AddRange(IEnumerable<T>)`, which snapshots its input and emits at most one
reset, and `Replace(IEnumerable<T>)`, which reconciles a complete next snapshot.
It also exposes direct reload notifications:

```csharp
Results[index].Status = "Connected";
Results.ReloadItem(index); // re-evaluate only this occurrence

_iconCache.Clear();
Results.ReloadItems();     // re-evaluate every occurrence
```

Both methods retain the renderer's normal keyed reuse and patching. A specific
reload lets the common non-virtual keyed path re-invoke that row's `{Function
...}` expressions without re-evaluating unaffected rows or replacing its
compatible control tree. Its index is the current zero-based logical index in
the binding. Cases that cannot be patched locally use the existing
transactional refresh fallback. A whole-binding reload reaches every
`ItemsControl` that observes the binding; `ui.ReloadItems("Results")` remains
the named-host alternative.

The binding also exposes item-aware scrolling:

```csharp
Results.ScrollIntoView(result); // first equal occurrence, Nearest
Results.ScrollIntoView(result, ItemScrollAlignment.Center, true);
Results.ScrollIndexIntoView(index, ItemScrollAlignment.Start, false);
```

The item overload uses `EqualityComparer<T>.Default` and selects the first
match. The explicitly named index overload is unambiguous for
`ItemsBinding<int>` and addresses duplicate occurrences exactly. Requests use
weak observers and intentionally reach every host displaying the same binding;
call `ui.GetItemsControl("Results").ScrollIntoView(...)` to target only one
host. Source replacement and disposal detach that host. A request waits for a
queued `ListChanged` patch before executing. The item overload re-resolves its
first equal occurrence from the committed list and becomes a no-op if that item
was removed; the index overload retains its numeric index. Cross-thread request
bursts are coalesced independently by each host, newest request first, through
one pending owner-thread dispatch.

The `IList<T>` constructor copies its initial values instead of retaining and
mutating the caller's list.
Self-add is snapshotted, self-replace is an O(1) no-op, and a source-enumeration
failure happens before the list is changed. An identical replacement emits
nothing. Small inserts, removals, replacements, and moves emit only their matching
`ListChanged` notifications; duplicate references are retained correctly. A
longest-increasing-subsequence reorder plan keeps rotations and other mostly
ordered permutations small. Large unrelated diffs use one bounded reset rather
than an unbounded edit calculation. Reference types are matched by identity and
value types by value.
The control observes
any `IBindingList` with `SupportsChangeNotification`; changes are coalesced into
one owner-thread update. Exact bounded event batches avoid source enumeration
and use the normal transactional keyed renderer; unverifiable events use a full
reload. Source replacement and runtime disposal detach the old list, and
pre-handle notifications are applied after handle creation.
`PropertyBinding<T>` item fields patch only their affected realized binding
slots. Existing notification-based items remain supported. Replacing a list
cannot detect a member changed inside the same snapshot instance; use a reactive
item value or call `ReloadItem(index)` for that case. When `ItemVersionPath` is
present, increment its value before `ReloadItem`; an unchanged explicit version
authorizes the renderer to retain ordinary output. With the default
`ReevaluateFunctionsOnRefresh`, Function values are recalculated even when the
explicit item version is unchanged.

An ordinary `IEnumerable` is a non-observable fallback. Assign and refresh it
with the manual API:

```csharp
ui.SetItems("Results", results);

results.Add(new SearchResult("Another result"));
ui.ReloadItems("Results");

ui.GetItemsControl("Results").ScrollToIndex(500);
ui.GetItemsControl("Results").ScrollIntoView(
    500,
    ItemScrollAlignment.Center,
    true);
```

`ForceReloadItems` is for output that depends on untracked external state. Use
the less expensive `ReloadItems` for normal item property and collection
changes.

The returned `ItemsControl` also exposes:

- configuration properties for keys, versions, progressive rendering,
  immediate or smooth scrolling, nullable `VerticalScrollStyle` and
  `HorizontalScrollStyle` objects, virtualization threshold, direction-aware
  fixed-budget overscan, estimated or fixed item size, and bounded same-item
  caching;
- live `Count`, `RealizedCount`, `VirtualCacheCount`, `IsVirtualizing`, and
  `IsRefreshing` state;
- the current `VirtualRealizedStartIndex`/`VirtualRealizedEndIndex` and
  allocation-free lifetime counters: `VirtualRetainedReuseCount`,
  `VirtualCacheReuseCount`, `VirtualCrossItemRecycleCount`,
  `VirtualCrossItemRecycleRejectedCount`, `VirtualCreatedCount`, and
  `ProgressiveBatchCount`;
- allocation-free lifetime construction counters:
  `ItemTemplateBlueprintBuildCount`, `ItemTemplateFallbackBuildCount`, and
  `ItemControlTreeDisposedCount`;
- the current allocation-free `ActiveItemBindingSubscriptionCount` across
  prepared Controls and Lightweight rows;
- `RefreshCompleted` and `RefreshFailed` events;
- `LastRefreshError`;
- `ScrollToStart()` and the retained immediate leading-edge
  `ScrollToIndex(index)` shortcut;
- `ScrollIntoView(index)`, `ScrollIntoView(index, alignment)`, and
  `ScrollIntoView(index, alignment, animate)` with `Nearest`, `Start`,
  `Center`, or `End` alignment.

`ItemsControl.AutoScroll` defaults to `true`; `SmoothScroll` defaults to
`false`. Wheel and line/page commands therefore move the live control tree
immediately through the active native or framework-owned scrollbar. Setting
`SmoothScroll=true` explicitly coalesces those commands into one transition
whose `SmoothScrollDuration` defaults to 120 milliseconds. Repeated commands retarget
that transition without resetting its fractional position or velocity, and
sub-notch wheel deltas move proportionally instead of being held for a complete
notch. Unhandled navigation keys from focused item descendants scroll the
nearest item host; editors and nested controls keep keys they consume. A native
thumb follows the operating-system live-content
preference when `LiveScroll=false`; `LiveScroll=true` forces content to follow
tracking even when that preference is disabled. Native thumb release always
commits immediately. A framework thumb cancels an active transition too:
`LiveScroll=true` updates content while it moves, while `LiveScroll=false`
moves only the thumb until release commits the selected offset.
The default `SmoothScroll=false` path does not capture or replace themed item
content with a bitmap; the logical position, live controls, and active thumb
are published together.

`ScrollIntoView` without an explicit animation argument follows `SmoothScroll`.
Its three-argument overload is a per-call override: `true` animates even when
the property is false, and `false` moves immediately even when it is true.
Alignment is logical, so horizontal RTL maps `Start` to the right and `End` to
the left without application-side coordinate conversion. A shared
`ItemsBinding<T>` broadcasts its convenience request to every observing host;
host-specific calls move only that view. A running item-aware transition
recalculates its target after wrap reflow and after variable-size virtualization
publishes measured extents, so its final alignment does not retain estimated or
pre-resize geometry.

`VerticalScrollStyle` and `HorizontalScrollStyle` default to null. Null preserves
the native scrollbar. A non-null value selects framework-owned chrome only when
its axis matches `Orientation`; the other value remains assigned but is not
instantiated or shown. Both ordinary and virtualized renderers use the same
active scrollbar contract. `KeepScrollBarOnRight=true` keeps a vertical framework
bar on the right in RTL; otherwise vertical placement follows content direction.
The horizontal bar mirrors geometry and input in RTL without changing logical
offsets. `ItemsControl.ScrollBarGap` defaults to zero and reserves that many
pixels between item content and either a native or framework-owned scrollbar.
The gap follows the bar on vertical RTL placement and works identically for
horizontal hosts; changing it relayouts retained rows without recreating them.

Eligible virtualized lists realize each visible logical-index range
synchronously on the UI thread. `ProgressiveRendering` applies to the normal
renderer and does not defer a direct virtual viewport change.
`OverscanItems=N` keeps the existing maximum budget of `2*N` extra rows. An
initial Controls viewport splits that budget evenly. Once the native origin
moves, the same budget is biased ahead of travel; duplicate native callbacks at
the settled offset retain the published bias, so they do not replace an
otherwise identical committed range. Large jumps and end-of-content clamps
still publish every final visible row before the scroll event returns.

`ItemRecycling` defaults to `Disabled`, so a detached cached control tree is
reused only for the same item identity/key. `ItemRecycling=Explicit` is limited
to `VirtualizationMode=Controls` and additionally requires the item-template
root to implement `IRecyclableItemControl`. The callback receives an immutable
`ItemRecycleContext` while the root is detached and item subscriptions are
inactive. Returning `false` disposes that candidate and constructs a fresh row;
throwing fails the refresh visibly. Arbitrary control trees are never inferred
to be safe. `VirtualCacheReuseCount` counts every successfully published
detached-cache reuse; `VirtualCrossItemRecycleCount` is its precise cross-item
subset.

See the [ItemsControl guide](/guide/items-and-virtualization) for complete
examples and tuning advice.

## Use ProgressBar

XML uses the canonical `ProgressBar` element:

```xml
<ProgressBar Name="Loading"
             Style="Marquee"
             MarqueeAnimationSpeed="35" />
```

Lookup uses the normal WinForms type:

```csharp
ProgressBar loading = ui.Get<ProgressBar>("Loading");
loading.Style = ProgressBarStyle.Marquee;
loading.MarqueeAnimationSpeed = 35;
```

The control uses native marquee when the application is rendering controls
with visual styles. Otherwise a timer drives the built-in native Blocks control:
the bar grows from one side, then drains while the remaining blocks stay
anchored on the opposite side. The main native HWND owns one unchanged border
throughout the cycle. Both segments reveal complementary regions of one fully
filled private native progress HWND clipped only to the track interior, so
their native blocks and bevels are identical. This is not owner drawing and
does not add a managed child to `Controls`.
`ProgressBar.Style`, `MarqueeAnimationSpeed`, `Minimum`, `Maximum`, and `Value`
remain the public logical state.

To preview or deliberately select that fallback on a capable system:

```xml
<ProgressBar Name="FallbackPreview"
             Style="Marquee"
             PreferMarqueeFallback="true" />
```

The default `false` selects the fallback only when native marquee is
unavailable. Call `Application.EnableVisualStyles()` before creating forms when
supported systems should use native marquee. The inherited
`MarqueeAnimationSpeed` value is passed to native marquee unchanged. The Blocks
fallback advances every three times that interval, so it runs at one-third the
requested cadence (`35` becomes a 105 ms fallback frame interval). A speed of
zero pauses at the current phase.

If the same progress bar also uses a keyed markup style, select that resource
with `ResourceStyle`; `Style` remains the native `ProgressBarStyle` property.

## Use framework-owned scrollbars

Use standalone `VerticalScrollBar` or `HorizontalScrollBar` when the application
must own the complete scrollbar appearance outside an `ItemsControl`. Native
`VScrollBar` and `HScrollBar` remain available as separate built-ins.

```xml
<VerticalScrollBar Name="ResultsScroll"
                   Minimum="0"
                   Maximum="999"
                   LargeChange="100"
                   SmallChange="20"
                   Value="{Binding ResultsOffset, Mode=TwoWay}"
                   TrackColor="{Preset Theme.ScrollTrack}"
                   ThumbColor="{Preset Theme.ScrollThumb}"
                   ThumbHoverColor="{Preset Theme.ScrollThumbHover}"
                   Thickness="14"
                   Scroll="ResultsScroll_Scroll" />
```

Lookup returns the framework control directly:

```csharp
VerticalScrollBar scroll =
    ui.Get<VerticalScrollBar>("ResultsScroll");

scroll.ValueChanged += ResultsScroll_ValueChanged;
scroll.Value = 200;
```

The public range follows the native WinForms contract: the effective last
value is `Maximum - LargeChange + 1`. User input raises `Scroll`, a changed
value then raises `ValueChanged`, and a completed mouse interaction raises an
`EndScroll` event. Programmatic `Value` assignment raises `ValueChanged` but
does not pretend to be user scrolling.

Assign a shared style from code when several controls use the same live theme:

```csharp
ScrollBarStyle shared = new ScrollBarStyle();
shared.TrackColor = Color.FromArgb(32, 33, 36);
shared.ThumbColor = Color.FromArgb(128, 134, 139);

ui.Get<VerticalScrollBar>("ResultsScroll").Style = shared;
ui.Get<HorizontalScrollBar>("TimelineScroll").Style = shared;
```

Changing `shared` invalidates both controls. The style subscription and the
single press-repeat timer are released when each control is disposed.

The same object can style the active axis of an item host:

```csharp
ItemsControl results = ui.GetItemsControl("Results");
results.VerticalScrollStyle = shared;
results.ScrollBarGap = 8;

// Restores the native scrollbar for a vertical Results host.
results.VerticalScrollStyle = null;
```

`ScrollBarGap` remains on `ItemsControl` because the host owns the relationship
between its content viewport and either scrollbar renderer. Reusing the same
style object never changes layout spacing.

XML can construct the object with
`ItemsControl.VerticalScrollStyle`/`ItemsControl.HorizontalScrollStyle`, or bind
the matching attribute to a `ScrollBarStyle` supplied by code. See the
[ItemsControl guide](/guide/items-and-virtualization#style-the-active-scrollbar)
for complete preset, smooth-scroll, virtualization, and RTL examples.

## Use presets from `XmlForm`

Declare preset variants inline or in a referenced XML resource. Select a
declared variant through the protected `XmlForm` shortcut:

```csharp
Presets.Select("Theme", "Dark");
```

Keep changing scalar values as stable `PropertyBinding<T>` fields on the form
and reference them from preset declarations:

```xml
<Set Key="Accent" Value="{Binding AccentColor}" />
```

This is the recommended state path. See [Dynamic presets](/guide/presets).

## Lifetime

The shortest `XmlForm` startup form is:

```csharp
new MainForm().Start();
```

`Start()` runs the native form's message loop. Closing and disposing that form
releases the paired `XmlForm` and runtime. Application code can also call
`XmlForm.Dispose()` explicitly; it disposes the retained runtime. When
application code owns a runtime directly, dispose that runtime instead:

```csharp
using (XamlRuntime ui = XamlRuntime.Load(xml, codeBehind))
{
    Application.Run(ui.Form);
}
```

Disposal is idempotent. It releases runtime-owned images, icons, fonts, helper
objects, markup event registrations, pending item refresh work, active
`PropertyBinding<T>` and observed item-list subscriptions, and subscriptions to
its preset sources. The runtime also owns and disposes the root created
from its XML, including a non-Control root that implements `IDisposable`.
Application code remains responsible for objects it supplied through bindings
or ordinary property assignment.

After successful disposal, `IsDisposed` is `true`, `Root` and `RootControl` are
`null`, and `Names` and `NamedObjects` are empty. If cleanup throws,
`IsDisposed` remains `false` and the runtime retains only the state needed for a
later `Dispose()` retry. Repeated calls after successful disposal are harmless.

If a custom event accessor throws while removing a runtime handler, that handler
is disabled immediately. A later `Dispose()` call retries the physical removal
without reactivating the callback. The same rule protects disposal tracking for
non-root binding targets.
