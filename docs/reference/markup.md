# Markup reference

WinFormsXaml accepts well-formed XML and creates .NET objects. Native Windows
Forms elements and properties use their normal CLR names.

The packaged `WinFormsXaml.xsd` describes the built-in vocabulary for Visual
Studio IntelliSense. Add a standard `xsi:noNamespaceSchemaLocation` association
to a form, component, or preset document; the runtime ignores the schema
metadata. See [XML IntelliSense with the XSD schema](/guide/xml-intellisense).

## Native control elements

The resolver first looks for public types in `System.Windows.Forms`, so these
elements create the matching native types:

```xml
<Form />
<Panel />
<Label />
<TextBox />
<Button />
<CheckBox />
<RadioButton />
<ComboBox />
<ListBox />
<ListView />
<TreeView />
<PictureBox />
<TrackBar />
<TabControl />
<TabPage />
<DataGrid />
<DataGridView />
<BindingNavigator />
<MonthCalendar />
<DateTimePicker />
<MenuStrip />
<ToolStrip />
<ToolStripContainer />
<ToolStripPanel />
<StatusStrip />
<ToolBar />
<StatusBar />
<ToolStripStatusLabel />
<ToolStripProgressBar />
<WebBrowser />
<PrintPreviewControl />
<PrintPreviewDialog />
```

The same rule applies to other public WinForms types with a parameterless
constructor.

## WPF-style image element

`Image` is the concise image element. It creates an extensible
`WinFormsXaml.ImageControl`, which derives from `PictureBox`, defaults to
aspect-preserving `Stretch="Uniform"`, and accepts the existing mapped
`Source` path:

```xml
<Image Source="{Binding Thumbnail}"
       Stretch="Uniform"
       Width="120"
       Height="80" />
```

`Source` may be a literal file path or a Binding, Function, or Preset that
returns a path, `System.Drawing.Image`, `Icon`, or encoded `byte[]`.
`SourceChanged` and `StretchChanged` are the corresponding `ImageControl`
events. `SourceChanged` also observes effective binding, function, preset, and
literal-path reloads; an assignment that leaves both the installed image and
image location unchanged does not raise a duplicate event or restart native
loading. Call the inherited `Load()` or `LoadAsync()` API to deliberately
refresh unchanged location text. A reloaded encoded `byte[]` is fingerprinted,
so edits made in place invalidate the decoded-image cache without retaining a
second byte copy. Controls using the same `Icon` instance also share its single
weakly cached bitmap conversion instead of allocating one bitmap per control.
Externally disposing a bound image control immediately releases its runtime
ownership reference; a shared conversion is disposed only after its last live
target releases it. Application-provided images are never disposed by this path.

Use `PictureBox` when the exact native type and native property names are more
useful. It keeps the native `Normal` size-mode default and completes its
asynchronous loading surface:

```xml
<PictureBox ImageLocation="{Binding PreviewPath}"
            InitialImage="{Binding LoadingImage}"
            ErrorImage="{Binding MissingImage}"
            WaitOnLoad="{Binding LoadSynchronously}"
            SizeMode="{Preset Media.PictureMode}"
            LoadCompleted="Preview_LoadCompleted" />
```

`Image`, `ImageLocation`, `InitialImage`, `ErrorImage`, `WaitOnLoad`, and
`SizeMode` all accept dynamic expressions. Native `LoadCompleted`,
`LoadProgressChanged`, and `SizeModeChanged` events remain available. The
runtime also accepts the mapped `Source` and `Stretch` aliases on a
`PictureBox`, but the element still creates exactly
`System.Windows.Forms.PictureBox`; use `Image` when the custom
`SourceChanged`/`StretchChanged` events or the `Uniform` default are desired.

Both element forms share the same decode cache, native location loader, animated
image updates, and runtime-owned image lifetime. `UniformToFill` adds an exact,
centered cover crop over that PictureBox pipeline. It draws the existing image
directly and recalculates only the source rectangle after a resize; it never
creates or retains a resized bitmap.

Collection objects nest under the native owner that exposes their collection:

```xml
<StackPanel Orientation="Vertical">
  <TreeView>
    <TreeNode Text="Products">
      <TreeNode Text="Current" />
    </TreeNode>
  </TreeView>

  <DataGridView AutoGenerateColumns="false">
    <DataGridViewTextBoxColumn HeaderText="Name"
                               DataPropertyName="Name" />
    <DataGridViewCheckBoxColumn HeaderText="Active"
                                DataPropertyName="IsActive" />
  </DataGridView>
</StackPanel>
```

The same child logic covers ListView items, groups, and column headers; .NET 2
`DataGrid` table/column styles; ToolBar buttons; StatusBar panels; and ToolStrip
status/progress items.

## WinFormsXaml elements

| Element | Purpose |
| --- | --- |
| `Grid` | Row and column layout. |
| `StackPanel` | Vertical or horizontal stack layout. |
| `DockPanel` | Edge docking with optional last-child fill. |
| `Canvas` | Coordinate-based layout. |
| `FlexPanel` | Flexible row or column layout with optional wrapping. |
| `Border` | Single child with a painted border. |
| `ScrollViewer` | Scrollable single-child host. |
| `Viewbox` | Single-child host. |
| `TabView` | Native tabs by default, with adaptive framework-painted chrome when effective custom appearance is applied. |
| `TabViewItem` | One header and zero or one visual page-content child. |
| `ItemsControl` | Data template repetition, optional virtualization, and optional active-axis scrollbar styling. |
| `Includes` | Standalone reusable markup root or condition-capable include directive with a static source. |
| `ProgressBar` | Native WinForms progress with automatic legacy marquee fallback. |
| `HyperlinkLabel` | LinkLabel with a bindable URI opened when the link is activated. |
| `VerticalScrollBar` | Framework-owned, fully styleable vertical scrollbar. |
| `HorizontalScrollBar` | Framework-owned, fully styleable horizontal scrollbar with RTL mapping. |

### TabView

`TabView` uses native `System.Windows.Forms.TabControl` chrome while its
appearance values remain at their defaults. An effective custom appearance
switches it to the framework-painted surface, removing the native white
header/frame without an application `DrawItem` handler. The transition reuses
the same page objects and preserves handles, selection, bindings, and focus.
`ForceNativeTabs="true"` keeps the native surface active even when custom
appearance values are stored.

Declare `TabViewItem` pages directly or in one `TabView.TabItems` property
element. The forms are equivalent, and each item accepts at most one direct
visual child:

```xml
<TabView Name="DetailsTabs"
         SelectedIndex="{Binding ActiveTab, Mode=TwoWay}"
         SelectionChanged="DetailsTabs_SelectionChanged">
  <TabViewItem Header="Summary">
    <Panel />
  </TabViewItem>
  <TabViewItem Header="History">
    <Panel />
  </TabViewItem>
</TabView>
```

```xml
<TabView>
  <TabView.TabItems>
    <TabViewItem Header="Summary"><Panel /></TabViewItem>
    <TabViewItem Header="History"><Panel /></TabViewItem>
  </TabView.TabItems>
</TabView>
```

`TabView.Resources` and `TabViewItem.Resources` provide the normal local style
and preset scopes and do not count as page content. A direct non-item child and
a second visual child inside one item are markup errors.

The style surface is:

- Color: `TabBackground`, `SelectedTabBackground`, `TabForeground`,
  `SelectedTabForeground`, `TabBorderBrush`, `ContentBackground`, and
  `ContentBorderBrush`;
- Padding: `TabBorderThickness`, `TabPadding`, `ContentBorderThickness`, and
  `ContentPadding`;
- nonnegative integer: `HeaderSpacing` and `TabCornerRadius`;
- integer at least `-1`: `SelectedTabCornerRadius`, where `-1` inherits
  `TabCornerRadius`.

Every value above accepts literals, Binding, Function, and Preset expressions,
and style setters. Four-part padding/thickness is the physical
`left,top,right,bottom` order in LTR and RTL. `BackColor` fills unused outer and
header-strip space.

Every appearance property has its corresponding `<PropertyName>Changed` event.
For example, `TabBackgroundChanged`, `TabPaddingChanged`,
`HeaderSpacingChanged`, `ContentBorderThicknessChanged`,
`TabCornerRadiusChanged`, and `SelectedTabCornerRadiusChanged` are normal markup
event attributes. `ForceNativeTabsChanged` is also available, and the packaged
XSD completes the full surface.

`TabItems` is a read-only property returning the mutable
`TabViewItemCollection`. It supports indexing, add, insert, remove, remove-at,
clear, contains/index queries, and `Move(oldIndex,newIndex)`. `SelectedIndex`
and `SelectedItem` support two-way binding. `SelectedIndexChanged` and
`SelectedItemChanged` are the focused property events; `SelectionChanged`
provides `OldIndex`, `NewIndex`, `OldItem`, and `NewItem` in one
`TabViewSelectionChangedEventArgs` value. A declarative `SelectedIndex` is
resolved after all sibling item declarations are attached, regardless of XML
attribute order.

Direction uses only `FlowDirection="LeftToRight|RightToLeft"` or the inherited
native `RightToLeft="No|Yes|Inherit"` contract. In LTR, logical item zero is
leftmost; in RTL it is rightmost. `TabItems` and selected indexes remain in
logical order. Effective direction inherits through a form, ordinary layout
containers, and registered components, and reloading a bound direction
relayouts the existing headers without rebuilding or reversing the collection.
Left/Right keyboard navigation follows physical adjacency among eligible
headers; Home/End and Ctrl+Tab traversal preserve logical first/last and
forward/backward semantics.

### Framework-owned scrollbars

`VerticalScrollBar` and `HorizontalScrollBar` are standalone owner-painted
controls whose track, thumb, arrows, border, and thickness can be styled without
a `DrawItem` handler. They are distinct from native `VScrollBar` and
`HScrollBar`:

```xml
<VerticalScrollBar Name="DocumentScroll"
                   Minimum="0"
                   Maximum="1000"
                   LargeChange="120"
                   SmallChange="24"
                   Value="{Binding Offset, Mode=TwoWay}"
                   TrackColor="{Preset Theme.ScrollTrack}"
                   ThumbColor="{Preset Theme.ScrollThumb}"
                   ThumbHoverColor="{Preset Theme.ScrollThumbHover}"
                   ArrowColor="{Preset Theme.ScrollArrow}"
                   BorderColor="{Preset Theme.Border}"
                   Thickness="14" />
```

Use a nested style when the settings should be handled as one object:

```xml
<HorizontalScrollBar Name="TimelineScroll"
                     Maximum="500"
                     LargeChange="50">
  <HorizontalScrollBar.Style>
    <ScrollBarStyle TrackColor="{Preset Theme.ScrollTrack}"
                    ThumbColor="{Preset Theme.ScrollThumb}"
                    ThumbHoverColor="{Preset Theme.ScrollThumbHover}"
                    ThumbPressedColor="{Preset Theme.ScrollThumbPressed}"
                    ArrowColor="{Preset Theme.ScrollArrow}"
                    ArrowHoverColor="{Preset Theme.ScrollArrowHover}"
                    BorderColor="{Preset Theme.Border}"
                    Thickness="14"
                    MinimumThumbLength="10" />
  </HorizontalScrollBar.Style>
</HorizontalScrollBar>
```

Every color and metric above accepts a literal, Binding, Function, or Preset
expression. `Style` is the real `ScrollBarStyle` CLR property on these controls;
use `ResourceStyle` to select a keyed markup style. One `ScrollBarStyle` object
can also be assigned to several controls from C#, and its `Changed` event makes
all of them repaint.

`ItemsControl` can integrate the same style object into its own scroll host. The
properties are named for the logical axis, and the nested syntax accepts exactly
one `ScrollBarStyle`:

```xml
<ItemsControl Orientation="Vertical"
              ItemsSource="{Binding Results}"
              ScrollBarGap="8">
  <ItemsControl.VerticalScrollStyle>
    <ScrollBarStyle TrackColor="{Preset Theme.ScrollTrack}"
                    ThumbColor="{Preset Theme.ScrollThumb}"
                    ThumbHoverColor="{Preset Theme.ScrollThumbHover}"
                    ThumbPressedColor="{Preset Theme.ScrollThumbPressed}"
                    ArrowColor="{Preset Theme.ScrollArrow}"
                    ArrowHoverColor="{Preset Theme.ScrollArrowHover}"
                    BorderColor="{Preset Theme.Border}"
                    Thickness="14"
                    MinimumThumbLength="10" />
  </ItemsControl.VerticalScrollStyle>
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`ItemsControl.HorizontalScrollStyle` is the equivalent property element for
`Orientation="Horizontal"`. The object-valued attributes
`VerticalScrollStyle="{Binding ScrollStyle}"` and
`HorizontalScrollStyle="{Function CreateScrollStyle()}"` are also supported;
Binding, Function, and Preset forms must resolve to a real `ScrollBarStyle` or
null. There is no string object literal—use the nested form for an inline style.

Omitted/null preserves the native scrollbar. A non-null style selects the
framework-owned scrollbar only for the axis matching `Orientation`; a configured
inactive-axis style is retained without creating a second bar. This invariant is
the same for ordinary, Controls-virtualized, and Lightweight item hosts.
`ScrollBarGap` reserves empty pixels between item content and either the native
or framework-owned active bar. It defaults to zero and works on either axis and
on either vertical side in RTL.
`SmoothScroll` controls wheel, arrow, and page commands for either scrollbar;
`LiveScroll` controls whether a framework thumb drag updates content during the
drag or only on release. `KeepScrollBarOnRight` overrides the vertical placement
in RTL, while a horizontal framework scrollbar mirrors geometry and input.

The controls support arrow and page repeat, thumb drag, mouse wheel, Home/End,
Page Up/Down, and arrow keys. A horizontal control maps minimum to the right in
effective RTL mode while its logical value range stays unchanged. Like native
WinForms scrollbars, the maximum reachable `Value` is
`Maximum - LargeChange + 1`. Painting uses one managed control and no child
window per scrollbar.

`HyperlinkLabel` keeps the normal LinkLabel appearance surface and adds
`NavigateUri`. Use either native `Text` or the `Content` convenience mapping
for its visible label:

```xml
<HyperlinkLabel Text="Open documentation"
                NavigateUri="{Binding DocumentationUri}"
                LinkBehavior="HoverUnderline" />
```

`NavigateUri` accepts literal, Binding, Function, and Preset values. Native
properties such as `LinkArea`, `LinkColor`, `VisitedLinkColor`, and
`LinkVisited` remain available normally. Activation raises the native
`LinkClicked` event and then opens the URI through the operating system's
default application. An empty URI only raises the native event. To replace
automatic navigation, handle `RequestNavigate` and set
`HyperlinkNavigateEventArgs.Handled` to `true`; the event receives the URI
captured when activation began.

For several independently clickable ranges, keep the native `LinkLabel` and
declare its normal `Links` collection:

```xml
<LinkLabel Text="Docs and support" LinkClicked="HelpLink_Click">
  <LinkLabel.Links>
    <Link Start="0" Length="4" LinkData="docs" />
    <Link Start="9" Length="7" LinkData="support" />
  </LinkLabel.Links>
</LinkLabel>
```

See the [Flex layout guide](/guide/flex-layout) for weighted growth, wrapping,
alignment, responsive rows and columns, and container-selection examples.

## Names

`Name` registers an object in the runtime's case-insensitive global name map:

```xml
<TextBox Name="CustomerName" />
```

```csharp
TextBox customer = ui.Get<TextBox>("CustomerName");
```

Template names are scoped to each repeated template and are not global.
`Name` defines static identity. Its value must be a literal and cannot contain
binding, function, or preset expressions.

## Form Class

`Class` declares the static CLR type that supplies form bindings and events:

```xml
<Form Class="MyProduct.UI.MainForm"
      Name="MainForm" />
```

When an `XmlForm` code-behind object is supplied, its type must match. Normal
applications create that typed form directly:

```csharp
new MainForm().Start();
```

Direct embedded-runtime loading can create a concrete declared class with a
public parameterless constructor when no code-behind object is supplied.

`Class` is identity metadata and cannot contain a binding, function, or preset
expression.

Use these native WinForms `Form` properties for resize behavior:

```xml
<Form FormBorderStyle="FixedDialog"
      MaximizeBox="false"
      MinimizeBox="true" />
```

`FormBorderStyle` accepts `None`, `FixedSingle`, `Fixed3D`, `FixedDialog`,
`Sizable`, `FixedToolWindow`, or `SizableToolWindow`.

## Registered component elements

Application element names can be registered globally:

```csharp
using WinFormsXaml;

XamlRuntime.Register("ActionButton", typeof(ActionButton));
XamlRuntime.Register("UI.Components");
```

```xml
<ActionButton Text="Save" />
<StatusBadge Text="{Binding StatusText}" />
```

C# component attributes can satisfy public constructor parameters and writable
properties. Embedded XML components declare their accepted values with
`Component.Properties`. Declared XML-component properties are typed observable
locals, so an inner editor and the component invocation can both use
`Mode=TwoWay` to forward edits to a writable `PropertyBinding<T>`.
`Component Class="Namespace.Type"` optionally creates one public,
parameterless code-behind instance per invocation. Matching public
`PropertyBinding<T>` members are reused as the stable declared-property proxy;
matching plain members receive outer updates. The invocation element remains
the registered resource's short filename. See
[Reusable components](/guide/components).

An embedded component template may contain one empty `<Children />` below its
visual root. The invocation then accepts zero or more visual Controls at that
one insertion point. Each projected subtree keeps the consuming Form/current
item data context, caller namescope, event target, binding ownership, and source
diagnostics; the surrounding template uses declared component properties and
its optional component code-behind. Text projection, repeated slots, non-empty
slots, and slots inside item/property templates are rejected.

Optional code-behind may expose
`public readonly ChildrenBind Children = new ChildrenBind();`. It provides
snapshot enumeration and scoped `Get<T>`, transactional UI-thread `Replace`,
`Clear`, and `Wrap`. Successful attached replacement transfers ownership;
an identical reference-and-order replacement is a no-op; failed mutation
restores the previous tree; and pre-attach staged controls stay caller-owned
until attachment succeeds. `Changed` notifications reject recursive mutation
of that same `ChildrenBind`. The `Children` member is special only when the
template declares the slot; otherwise an unrelated public member with that name
is ignored.

A complete manifest resource name with exact casing wins. A differently cased
name that matches multiple case-only variants is ambiguous and lists its
candidates. Other fragment lookup errors use the same deterministic, bounded
candidate format. If multiple resources derive the same registered element
name, or a name conflicts with an existing registration, the exception reports
both resource/type origins.

## Native properties and events

Attributes resolve against writable CLR properties first and then WinForms
events:

```xml
<Button Name="SaveButton"
        Text="Save"
        Enabled="true"
        BackColor="White"
        ForeColor="Black"
        Padding="8,4"
        Click="Save_Click" />
```

Enums, colors, sizes, points, rectangles, padding values, booleans, numbers,
images, icons, and values supported by a .NET type converter are converted to
the destination type. Colors accept hex and ordinary named values plus the
qualified framework forms `Color.Red`, `Color.Transparent`,
`SystemColors.Control`, and every other public `Color` or `SystemColors` value.
The fully qualified `System.Drawing.Color.*` and
`System.Drawing.SystemColors.*` forms are accepted too.

Unsupported elements, properties, events, or conversions produce a structured
`WinFormsXamlLoadException`. Semantic property failures include the markup
source, deepest element path, and property name. Malformed XML includes parser
line and position with unchanged parser behavior. A semantic location points to
the exact failing attribute when retained, otherwise the deepest opening
element. Original coordinates survive item-template clones and registered
component `TemplateXml` round trips. See
[Runtime API: Inspect load failures](/reference/runtime#inspect-load-failures).

## Expressions

| Syntax | Meaning |
| --- | --- |
| `{Binding}` | Read the complete current data context. |
| `{Binding Title}` | Read `Title` from the current data context. |
| `{Binding Path=Title}` | Equivalent named-path form. |
| `{Binding Customer.Name}` | Read a nested property/field path. |
| `{Binding !IsReady}` | Read and negate a boolean-compatible value. |
| `{Binding Count >= 10}` | Evaluate a one-way numeric comparison. |
| `{Binding IsReady && !IsBusy}` | Evaluate a one-way logical expression. |
| `{Binding Title, Source=Current}` | Explicitly read the current form-markup, item, or component-local context. |
| `{Binding CanEdit, Source=CodeBehind}` | Read the original code-behind/event-target object from a nested context. |
| `{Binding Title, Mode=TwoWay}` | Read and write a terminal `PropertyBinding<T>`. |
| `{Binding Title, Mode=TwoWay, UpdateSourceTrigger=LostFocus}` | Defer target writeback until the Control loses focus. |
| `{Binding Title, Mode=TwoWay, UpdateSourceTrigger=Explicit}` | Defer target writeback until `UpdateBindingSource` is called. |
| `{Function FormatStatus}` | Prefer a zero-argument method, then a method receiving the current data context. |
| `{Function FormatStatus(State)}` | Call a method on the code-behind object. |
| `{Function GetImage(.)}` | Call a method with the current item. |
| `{Preset Theme.FormColor}` | Read the selected value from a preset set. |
| `{Preset Theme == Dark}` | Compare a set's selected preset name and return a reactive Boolean. |
| `{StaticResource PrimaryButton}` | Reference a named style where accepted. |

Explicit function arguments may be `.`, `DataContext`, `this`, `CodeBehind`,
`null`, a boolean, number, quoted string, or binding-style member path. Explicit
notifying path arguments make the complete function expression reactive.
`StaticResource` is not a general property expression; it is accepted for named
style selection and `Style.BasedOn`.

### Computed one-way bindings

A complete one-way binding may combine dotted paths, `.`, parentheses, quoted
strings, finite invariant-culture numbers, `true`, `false`, and `null` with
`!`, `<`, `<=`, `>`, `>=`, `==`, `===`, `!=`, `!==`, `&&`, and `||`.
Precedence from highest to lowest is primary/parentheses, `!`, relational,
equality, `&&`, then `||`; operators at one level associate left to right.
`===` and `!==` are aliases for `==` and `!=`, not separate coercing or
identity operators.

In XML attributes, write `<` as `&lt;` and `&&` as `&amp;&amp;`:

```xml
Condition="{Binding NumCount > 10}"
Condition="{Binding NumCount &lt;= 2}"
Condition="{Binding NumCount &lt; 2 &amp;&amp; NumCount > 0}"
Condition='{Binding TextContent === "Text" || TextContent == ""}'
Condition="{Binding doubleNum == 2.6}"
```

Comparisons do not coerce strings to numbers/Booleans or unrelated CLR types.
Numeric CLR types compare numerically across their CLR types; strings use
ordinal case-sensitive equality, and `""` means the actual empty string.
Relational operators require numeric operands. Logical operators use the same
boolean-compatible conversion as simple binding negation.

Every referenced path is resolved eagerly and every observable operand is
subscribed. A missing path therefore fails even on a branch that Boolean
short-circuiting could otherwise skip. `PropertyBinding<T>` and
`INotifyPropertyChanged` operands re-evaluate the full expression; snapshots
need an explicit reload. Computed bindings do not support `Mode=TwoWay`, method
calls, indexers, arithmetic, assignment, construction, or ternary expressions.
The bounded parser permits at most 1,024 expression characters, 256 tokens, and
32 nested parenthesis levels. See
[Comparison and logical expressions](/guide/bindings#comparison-and-logical-expressions)
for the complete contract and error behavior.

### Selected-preset Boolean expressions

A complete preset expression can compare selected preset names and can be used
on any Boolean-compatible dynamic property:

```xml
Condition="{Preset Theme == Dark}"
Enabled="{Preset Theme != Disabled}"
Checked="{Preset Theme == Dark &amp;&amp; Density == Compact}"
Condition='{Preset Theme == "High Contrast"}'
```

An unqualified identifier such as `Theme` names a preset collection and
resolves to its current `SelectedName`. A simple comparison value such as
`Dark` is an unquoted preset name; names containing spaces must be quoted.
String comparison is ordinal and case-insensitive.

The grammar supports `==`, `!=`, unary `!`, `&&`, `||`, and parentheses.
`Theme.CanEdit` resolves a key from the selected preset and configured default,
so Boolean keys can participate in compound expressions. XML attributes must
escape `&&` as `&amp;&amp;`. Selection changes and referenced-key mutations refresh
matching expressions automatically. Unknown collections fail with a located
markup error, while an ordinary false result is valid.

Preset Boolean evaluation is implemented within the C# 2.0/.NET Framework 2.0
surface and introduces no newer operating-system API dependency.

Bindings, functions, and presets can appear in string text:

```xml
<Label Text="Page {Binding PageNumber} of {Binding PageCount}" />
```

A direct expression can return a real CLR object:

```xml
<PictureBox Image="{Binding PreviewImage}" />
<Form Icon="{Binding CurrentIcon}" />
<Panel Padding="{Binding EditorPadding}" />
```

## Snapshot and reactive binding sources

Use a simple public field for a one-way snapshot, then explicitly reload its
consumer after mutation. This example is inside an `XmlForm`; directly retained
runtime code uses `ui.ReloadBinding(...)` instead:

```csharp
public string ManualStatus = "Ready";

private void UpdateManualStatus()
{
    ManualStatus = "Connected";
    ReloadBinding("ManualStatusLabel", "Text");
}
```

Use a stable readonly `PropertyBinding<T>` field for automatic refresh and
two-way editing. Markup unwraps `Value` and observes `ValueChanged`:

```csharp
public readonly PropertyBinding<string> Status =
    new PropertyBinding<string>("Ready");

private void UpdateStatus()
{
    Status.Value = "Connected";
}
```

```xml
<Label Name="ManualStatusLabel"
       Text="Snapshot: {Binding ManualStatus}" />
<Label Text="Reactive: {Binding Status}" />
```

Keep each wrapper stable. It provides thread-safe access and versioned ordering
when source and target edits compete. Existing models that implement
`INotifyPropertyChanged` remain supported for compatibility, including nested
path rebinding, but snapshot fields and stable wrappers are the canonical forms.

```csharp
private void UpdateHeader()
{
    // Header is a public readonly PropertyBinding<string> field.
    Header.Value = "new";
}
```

Writing `Header = "new"` cannot update a readonly wrapper field. Assign
`Header.Value` to preserve its subscribed identity.

Reactive wrappers work for direct values and interpolation. Runtime updates are
coalesced, dispatched to the runtime's WinForms owner thread, delayed until
handle creation when necessary, and detached with their owning binding or
runtime. A reactive non-Control root uses a private dispatcher while
`RootControl` remains null.

The `Source` option controls which object begins a binding path. Omitted
`Source`, or explicit `Source=Current`, preserves the current context.
`Source=CodeBehind` selects the active event target—normally the `XmlForm`
code-behind, or the optional per-invocation `Component.Class` inside that
component template. Nested item templates retain that same owner during later
refresh, virtualization, and bounded cache reuse. Projected `<Children />` markup keeps
the caller's event target. It never means the native `Form` control. Values are
case-insensitive; duplicates and unknown values are load errors, and
`CodeBehind` requires an event target.

`Mode=TwoWay` is allowed only on one complete binding expression whose path ends
in a writable `PropertyBinding<T>`. Existing writable notification-based CLR
properties remain supported for compatibility:

```xml
<TextBox Text="{Binding Status, Mode=TwoWay}" />
<CheckBox IsChecked="{Binding Accepted, Mode=TwoWay}" />
```

Snapshot fields are one-way. A `PropertyBinding<T>` endpoint writes through its
`Value`; the same `ValueChanged` event is visible to markup and application code.
The default `UpdateSourceTrigger` is `PropertyChanged`. `LostFocus` requires a
WinForms Control. `Explicit` is committed through
`XamlRuntime.UpdateBindingSource(targetOrName, property)`; both deferred modes
continue to receive source-to-target changes normally.

Native CLR property names are reversible. When the requested name is not itself
a property on the target, the reversible convenience aliases are `Content`,
`Header`, and `Title` for `Text`; `IsChecked` for `Checked`;
`IsEnabled` for `Enabled`; `IsReadOnly` for `TextBoxBase.ReadOnly`; and
`WebBrowser.Source` for `Url`. The runtime rejects a target without a writable
property. The default trigger also requires change notification. It also rejects
two-way negation, interpolation, style setters, attached properties, and
`ItemsSource`. Structural `Condition` bindings are also one-way only.

## Property elements

Use a dotted property element for nested content that cannot fit in an
attribute:

```xml
<Grid>
  <Grid.RowDefinitions>
    <RowDefinition Height="Auto" />
    <RowDefinition Height="*" />
  </Grid.RowDefinitions>
</Grid>
```

```xml
<ItemsControl Name="Results">
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

This is the only item-template grammar: `ItemsControl.ItemTemplate` is a direct
child of `ItemsControl` and contains exactly one direct visual root. Wrapper
elements and alternate template property names are rejected.

Template-local `Resources` form an isolated lexical scope derived from the
resources visible at the declaration site. That scope is retained by nested
`ItemsControl` templates for deferred rendering. Template `<Presets>` sources
and inline definitions keep their normal runtime-wide preset semantics, but
their XML definitions are imported once per compiled template rather than once
per item clone.

`ItemsSource` can be declared on the element. `ItemsBinding<T>` and any
notification-capable `IBindingList` refresh automatically; an ordinary
`IEnumerable` requires `ReloadItems` after its contents change:

```xml
<ItemsControl Name="Results"
              ItemsSource="{Binding Results}">
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

`ItemsControl` also exposes a retained, non-virtual wrapping layout. With
`Wrap="true"`, vertical orientation fills rows and scrolls vertically, while
horizontal orientation fills columns and scrolls horizontally. `Spacing` is
the item and line gap; `JustifyContent`, `AlignItems`, RTL direction, and a
non-negative `FlexGrow` on the item-template root use the same flex-line
semantics as `FlexPanel`. Literal layout values and dynamic expressions are
completed by the packaged XSD. A wrapped host cannot enable `Virtualizing`;
that combination is reported as an invalid configuration rather than silently
falling back to another renderer.

Leaf property-element text can also contain a retained expression:

```xml
<Label Name="Status">
  <Label.Text>{Binding StatusText}</Label.Text>
</Label>
```

The shipped schema also completes `ItemsControl.VerticalScrollStyle` and
`ItemsControl.HorizontalScrollStyle`, common collection property elements such as
`ListView.Columns`, `TreeView.Nodes`, `DataGridView.Columns`, `ToolStrip.Items`,
`TabControl.TabPages`, and `TabView.TabItems`, plus `Resources` on common native and custom
containers. Dotted properties on registered/application types remain supported
through the schema's lax extension boundary.

## Layout properties

The custom layout engine recognizes:

- `Margin`, `Padding`, `Width`, `Height`, minimum and maximum sizes; dimensions
  may be fractional or use `px`/`pt`, and `Width`/`Height` also accept `Auto`;
- horizontal and vertical alignment;
- `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, and `Grid.ColumnSpan`;
- row and column definitions using pixels, `Auto`, and proportional `*` units;
- `DockPanel.Dock` and `LastChildFill`;
- `Canvas.Left`, `Canvas.Top`, `Canvas.Right`, and `Canvas.Bottom`;
- `Panel.ZIndex`, where a higher integer brings an overlapping sibling nearer
  the front;
- stack orientation;
- flex direction, justification, alignment, wrapping, gap, and `FlexGrow`
  values; repeated `ItemsControl` content uses `Spacing` for both item and line
  gaps and keeps `Orientation` as its scroll axis;
- border color and thickness;
- `FlowDirection="LeftToRight|RightToLeft"`, distinct from the native
  `RightToLeft="No|Yes|Inherit"`, and inherited foreground/background behavior.

All `Control` elements also accept `ToolTip` text. The runtime shares and owns
the backing WinForms `ToolTip`, and a retained expression updates its text.

See [Markup and layout](/guide/markup-basics) for complete examples.

## Styles

Styles live inside a `Resources` property element:

```xml
<Form.Resources>
  <Style TargetType="Button">
    <Setter Property="Margin" Value="0,0,0,8" />
  </Style>

  <Style Key="PrimaryButton"
         TargetType="Button">
    <Setter Property="BackColor" Value="#2563EB" />
    <Setter Property="ForeColor" Value="White" />
  </Style>
</Form.Resources>
```

An unkeyed style with `TargetType` is implicit. Select a keyed style by key or
as a static resource:

```xml
<Button Text="Save" Style="PrimaryButton" />
<Button Text="Save" Style="{StaticResource PrimaryButton}" />
```

`Style` remains a native property when the control actually declares one. For
example, `<ProgressBar Style="Marquee" />` sets
`System.Windows.Forms.ProgressBar.Style`. To apply a named resource style to
such a control as well, use `ResourceStyle="ProgressBarTheme"`.

Styles can inherit another named style:

```xml
<Style Key="DangerButton"
       TargetType="Button"
       BasedOn="{StaticResource PrimaryButton}">
  <Setter Property="BackColor" Value="DarkRed" />
</Style>
```

`BasedOn` requires this canonical `{StaticResource Key}` form; a bare key is not
a style-inheritance expression.

Setter values can contain bindings, functions, and presets. Direct element
properties take precedence over style setters. When a dynamic style changes,
setters and markup-owned event handlers from the previous style are removed.

## Includes

Use `<Includes>` as the root of a reusable markup document. Use
`<Includes Source="..." />` inside a form, component, or another include to
expand that document before the object tree is built:

```xml
<Includes Source="SharedHeader" />
<Includes Source="MyProduct.UI.Shared.Footer.xml"
          SourceKind="EmbeddedResource" />
<Includes Source="UI/Shared/Overrides.xml" SourceKind="File" />
```

`SourceKind` is `Registered` by default and also accepts
`EmbeddedResource` or `File`. When omitted, an `embedded://` or `file://`
prefix selects the corresponding kind; an explicit conflict is rejected.
`Assembly` is an optional assembly name for an embedded resource. These
attributes are static composition metadata and do not accept markup
expressions. A standalone document can contain
`Includes.Resources`, normal `Presets`, nested includes, registered components,
and ordinary visual content.

See [Reusable includes](/guide/includes) for registration, source resolution,
nested composition, and the `XmlForm.Include` API.

## Conditions

`Condition` includes an element only when the result is true:

```xml
<Label Text="Ready"
       Condition="{Binding IsReady}" />

<Button Text="Retry"
        Condition="{Binding !IsReady}"
        Click="Retry_Click" />
```

A condition backed by `PropertyBinding<T>` is observed as a one-way binding. A
dynamic false element is retained in a collapsed state, so a false-to-true
source change shows it automatically. On an item-template
root, false excludes the item from layout and scroll extent. Virtual lists
observe the root condition for every item while keeping controls restricted to
the realized viewport; N distinct item sources can therefore require O(N)
subscriptions.

`Condition` also accepts the safe computed binding grammar described under
[Computed one-way bindings](#computed-one-way-bindings), including numeric and
string comparisons, parentheses, `!`, `&&`, and `||`. XML attributes must use
`&lt;` for `<` and `&amp;&amp;` for `&&`.

It also accepts [selected-preset Boolean expressions](#selected-preset-boolean-expressions),
for example `Condition="{Preset Theme == Dark}"`. The same preset expression
can target other Boolean properties such as `Enabled`, `Checked`, and `TabStop`.

Snapshot fields and functions without an explicit reactive path argument require
an explicit binding or item reload. `Condition` rejects
`Mode=TwoWay`. `Visibility` and conditions contributed by component invocations
and templates combine rather than replace one another; every active constraint
must permit display.

Resource metadata also accepts the correctly spelled `Condition` attribute:

```xml
<Style TargetType="Button" Condition="{Binding UseDarkControls}">
  <Setter Property="BackColor" Value="#23272E" />
  <Setter Property="ForeColor" Value="White"
          Condition="{Binding UseHighContrastText}" />
</Style>

<ItemsControl.VerticalScrollStyle Condition="{Binding UseCustomScrollBar}">
  <ScrollBarStyle TrackColor="#171A1F" ThumbColor="#596273" />
</ItemsControl.VerticalScrollStyle>
```

A false or unresolved style/setter condition contributes no values. Changes
restore the complete lower style layer before active styles are applied again,
so an old theme color cannot remain accidentally. Local XML properties keep
their normal higher precedence. A false conditional single-object property
element restores its underlying value; for an `ItemsControl` scrollbar style,
that normally restores `null` and native chrome. The condition belongs on the
dotted property element, not on the nested `ScrollBarStyle` object.

`Includes.Condition` accepts a Boolean literal or one-way Binding, Function, or
Preset expression. `Includes.Source`, `SourceKind`, and `Assembly` remain static
composition metadata and do not accept bindings. The source is composed once;
dynamic transitions retain visual and registered-component identity, included
resource styles restore their lower/native values while inactive, and nested
include conditions are ANDed. Preset declarations are imported as catalogs even
while the include is inactive. A conditional include cannot contribute a
top-level non-resource owner property element.

## Preset declarations

Inline:

```xml
<Presets Name="Theme" Selected="Light" Default="Light">
  <Preset Name="Light">
    <Set Key="FormColor" Value="White" />
  </Preset>
</Presets>
```

`Value` accepts literals, `{Binding ...}`, `{Function ...}`, and
`{Preset ...}`. A complete binding preserves typed CLR values and observes
`PropertyBinding<T>` paths automatically. Preset
bindings are one-way; nested preset references refresh transitively and cycles
are rejected. `Value` is required; use `Value=""` for an explicit empty string.
The only preset-container element name is `<Presets>`.

`{Preset SetName.Key}` reads a value. `{Preset SetName == PresetName}` instead
returns a reactive Boolean by comparing the set's selected name. See
[Selected-preset Boolean expressions](#selected-preset-boolean-expressions) for
operators, quoting, and error behavior.

Embedded resource:

```xml
<Presets Source="MyProduct.UI.ThemePresets.xml"
         SourceKind="EmbeddedResource" />
```

`Name` is required for an inline or standalone definition. It may be omitted on
a source-only loader because the referenced `<Presets>` document supplies the
set name.

File:

```xml
<Presets Source="Themes.xml" SourceKind="File" />
```

See [Dynamic presets](/guide/presets) for selection, fallback, sharing, and the
mutation/import API.

## Important binding rules

- `PropertyBinding<T>` values and explicit reactive function path arguments
  provide automatic one-way updates; snapshot values and functions without a
  discoverable path use explicit reloads.
- `Mode=TwoWay` writes to a terminal `PropertyBinding<T>` and is validated rather
  than silently degraded; legacy notification-based properties remain supported.
- Events are normal WinForms events, not routed events.
- Item-template names are local rather than globally addressable.
- `Condition` is structural, one-way, and reactive when it observes a
  `PropertyBinding<T>` path.
- `Name` is a static identity and cannot be bound.
- Invalid markup fails instead of being silently ignored.

Markup and preset XML should be trusted application resources. DTD declarations
are rejected and external entities are disabled, but loading can still create
types, set properties, resolve files, and connect code-behind methods.
