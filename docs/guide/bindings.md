# Reactive bindings and functions

A binding reads a public property or field from the C# object passed to `Load`
or `LoadEmbedded`. New code normally uses one of two forms: a simple public field
for snapshot state, or a stable readonly `PropertyBinding<T>` field for reactive
and two-way state.

The code-behind snippets use the protected reload shortcuts inherited from
`XmlForm`. Use `Ui` when another low-level `XamlRuntime` API is needed.

## The two canonical state forms

Use a simple public field when the value is a snapshot and application code
already knows when it changes:

```csharp
public string ManualCaption = "Ready";

private void UpdateManualCaption()
{
    ManualCaption = "Connected";
    ReloadBinding("ManualStatus", "Text");
}
```

```xml
<Label Name="ManualStatus" Text="{Binding ManualCaption}" />
```

Use a stable readonly `PropertyBinding<T>` field when changes should refresh
markup automatically, when control edits should write back, or when application
code needs a public change event:

```csharp
public readonly PropertyBinding<string> Header =
    new PropertyBinding<string>("Ready");

public readonly PropertyBinding<bool> IsReady =
    new PropertyBinding<bool>(false);

private void UpdateHeader()
{
    Header.Value = "new";
    IsReady.Value = true;
}
```

```xml
<Label Text="{Binding Header}" />
<TextBox Text="{Binding Header, Mode=TwoWay}" />
<Button Text="Start" Enabled="{Binding IsReady}" />
<ProgressBar Visible="{Binding !IsReady}" />
```

Bindings unwrap the wrapper automatically. Keep its instance stable and assign
through `.Value`; this preserves subscribers and raises `ValueChanged`. The
wrapper supplies a thread-safe value and an atomic version token used to order
competing source and target changes. No `Get`, reload call, or control-specific
update code is required for this form.

Existing models that already implement `INotifyPropertyChanged` remain
compatible. The package examples use public snapshot fields and stable readonly
`PropertyBinding<T>` fields so the update contract is visible at each declaration.

Direct bindings and interpolated text observe every `PropertyBinding<T>`
encountered while walking their paths:

```xml
<Label Text="User: {Binding Session.User.DisplayName}" />
```

When an observed dependency changes, the path is resolved again and obsolete
nested subscriptions are detached.

## Choose the current or code-behind source

An omitted `Source` reads the current context. That is normally the Form
code-behind object, the current item inside an `ItemsControl.ItemTemplate`, or
the declared local-value context inside an XML component. `Source=Current` is
the explicit equivalent.

Use `Source=CodeBehind` to reach shared Form state from a nested context:

```xml
<ItemsControl ItemsSource="{Binding Results}">
  <ItemsControl.ItemTemplate>
    <Button Text="{Binding Title}"
            Enabled="{Binding CanOpenResults, Source=CodeBehind}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

The selected source applies independently to each segment, so interpolation can
combine contexts:

```xml
<Label Text="{Binding Title} — {Binding Status, Source=CodeBehind}" />
```

`Current` and `CodeBehind` are case-insensitive. Code-behind means the object
passed as the runtime event target—normally the `XmlForm` instance—not the
native `System.Windows.Forms.Form`. Its `PropertyBinding<T>` values retain the
same automatic refresh, thread dispatch, and cleanup behavior. An explicit
code-behind source is rejected when no event target exists.

## Two-way binding

Add `Mode=TwoWay` to one complete binding expression when edits in the control
must update the terminal `PropertyBinding<T>`:

```xml
<TextBox Text="{Binding Header, Mode=TwoWay}" />
<CheckBox Checked="{Binding IsReady, Mode=TwoWay}" />
```

The initial value flows from the source to the control. A target change is
converted to the terminal type and assigned through the wrapper `Value`. Keep
the `PropertyBinding<T>` field readonly so the observed wrapper identity cannot
be replaced accidentally. Public snapshot fields remain one-way. Loop
suppression prevents feedback from bouncing indefinitely.

Target writeback is immediate by default. Use
`UpdateSourceTrigger=LostFocus` for editors whose conversion or downstream work
should wait until the native Control loses focus:

```xml
<TextBox Text="{Binding SearchText, Mode=TwoWay,
                UpdateSourceTrigger=LostFocus}" />
```

Use `UpdateSourceTrigger=Explicit` when application code owns the commit point:

```xml
<TextBox Name="DraftTitle"
         Text="{Binding Title, Mode=TwoWay,
                UpdateSourceTrigger=Explicit}" />
```

```csharp
UpdateBindingSource("DraftTitle", "Text");
// Direct-runtime equivalent:
ui.UpdateBindingSource("DraftTitle", "Text");
```

`PropertyChanged`, `LostFocus`, and `Explicit` affect only target-to-source
writeback; source changes remain reactive in every case. `LostFocus` requires a
real WinForms `Control`. `Explicit` can also be committed with the overload that
accepts the target object. Both update methods must run on the runtime's owner
UI thread and fail clearly when the property has no active TwoWay binding.

An edit raised on the Form's owner thread commits target-to-source before the
later WinForms interaction handler runs. For example, a `CheckBox` updates its
binding during `CheckedChanged`, so its subsequent `Click` handler reads the new
value from `IsReady.Value`. Source-to-target repainting of other controls remains
coalesced through the UI dispatcher. Worker-thread notifications are marshalled
as before.

The target may use its real CLR property name. When a requested name is not
itself a target property, these markup aliases are also reversible:

| Markup property | Native target property |
| --- | --- |
| `Content`, `Header`, or `Title` | `Text` |
| `IsChecked` | `Checked` |
| `IsEnabled` | `Enabled` |
| `IsTabStop` | `TabStop` |
| `IsReadOnly` | `TextBoxBase.ReadOnly` (`TextBox` and `RichTextBox`) |
| `Foreground` | `ForeColor` |
| `Background` | `BackColor` |
| `WebBrowser.Source` | `Url` |

For a native CLR property, the runtime normally subscribes through its
`PropertyDescriptor`; conventional `PropertyNameChanged` events therefore need
no framework-specific mapping. WinForms has a small set of editable properties
whose notification event uses a different name. These routes are also built in:

| Editable property | Change event used |
| --- | --- |
| `ComboBox.SelectedItem`, `ListBox.SelectedItem`, `CheckedListBox.SelectedItem` | `SelectedIndexChanged` |
| `ToolStripComboBox.SelectedItem` | `SelectedIndexChanged` |
| `Control.Width`, `Height` | `SizeChanged` |
| `Control.Left`, `Top` | `LocationChanged` |
| `TextBoxBase.Lines`, `ToolStripTextBox.Lines` | `TextChanged` |
| `RichTextBox.Rtf` | `TextChanged` |
| `DomainUpDown.SelectedIndex` | `SelectedItemChanged` |
| `MonthCalendar.SelectionStart`, `SelectionEnd`, or `SelectionRange` | `DateChanged` |
| `TreeView.SelectedNode` | `AfterSelect` |
| `TabControl.SelectedTab` | `SelectedIndexChanged` |
| writable `RichTextBox` selection properties | `SelectionChanged` |
| `Form.WindowState` | `SizeChanged` |
| `SplitContainer.SplitterDistance`, `Splitter.SplitPosition` | `SplitterMoved` |
| `WebBrowser.Url` | `Navigated` |
| `ScrollableControl.AutoScrollPosition` | `Scroll` |
| `PropertyGrid.SelectedObject` | `SelectedObjectsChanged` |
| `DataGridView.FirstDisplayedCell`, `FirstDisplayedScrollingColumnIndex`, `FirstDisplayedScrollingRowIndex`, `HorizontalScrollingOffset` | `Scroll` |

Several properties share one broader native event. The runtime snapshots each
bound target value and ignores an event when that specific property did not
change; for example, a vertical grid scroll does not claim an unchanged
horizontal-offset binding.

The runtime validates a two-way route while loading it. The recommended path
ends in a writable `PropertyBinding<T>`, and the target must expose a writable
property. The default `PropertyChanged` trigger also requires usable target
change notification; `LostFocus` and `Explicit` do not. Existing writable
notifying CLR properties remain supported for compatibility. `Mode=TwoWay` is
rejected for negated paths, interpolated text, style setters, attached
properties, `ItemsSource`, and `Condition`.

Writable properties without reliable change notification cannot use the
default trigger, but may use `LostFocus` or `Explicit` when that commit model is
appropriate. Examples include `ProgressBar.Value`, `DateTimePicker.Checked`,
plain `TextBox` selection properties, and `ComboBox.DroppedDown`.
`DateTimePicker.ValueChanged` cannot stand in for `Checked`: WinForms defines it
as a notification for `Value` and exposes no reliable `CheckedChanged` event.
Unknown modes, duplicate options, and missing paths are reported as markup
errors rather than silently becoming one-way bindings. If a temporary target
edit cannot be converted to `T`, the source remains unchanged and the control
keeps the editing value. A later valid edit can update the source normally.

Two-way binding can cross an XML-only component boundary. The component
template binds its editor to a declared property, and the invocation binds that
property to the form's terminal `PropertyBinding<T>`:

```xml
<!-- Editor.xml -->
<TextBox Text="{Binding Value, Mode=TwoWay}" />

<!-- consuming form -->
<Editor Value="{Binding Header, Mode=TwoWay}" />
```

Declared component properties use typed local observable values. A literal or
default remains editable inside the component; only an invocation explicitly
using `Mode=TwoWay` writes the local edit back to the form. See
[Reusable components](components.md#edit-a-component-property-in-both-directions).
Component invocation proxies use the immediate trigger; put `LostFocus` or
`Explicit` on the concrete editor binding inside the component.

## Thread dispatch and lifetime

`PropertyBinding<T>.Value` and its event list are thread-safe. `ValueChanged` is
synchronous on the thread that changes the value.

Runtime-owned subscribers coalesce source changes and marshal the resulting
update to the runtime's WinForms owner thread. `RootControl` is the dispatcher
when present; a reactive non-Control root uses a private dispatcher. If the
dispatcher handle does not exist yet, pending work waits for `HandleCreated`.
`PropertyBinding<T>` adds an atomic version check when source and target edits
compete.

A reactive non-Control root owns a private WinForms dispatcher. Dispose every
loaded runtime on its load thread. A wrong-thread attempt fails before mutating
runtime state, allowing an owner-thread retry.

Subscriptions are removed when their binding is replaced, a component root is
disposed, a styled value stops owning the property, an item-template instance
is detached into the virtual cache or discarded, or the runtime is disposed.
Dispose the runtime when the form closes.

Pooled source subscriptions and two-way target events use disable-capable
forwarders. If a custom event accessor stores a handler and then throws during
add, or throws during remove, the forwarder drops its callback before the
failure escapes. The publisher may retain an inert delegate, but not the
runtime, Form, or control graph behind it.

## Snapshot values and explicit reloads

Simple public fields are read as snapshots until an explicit reload:

```csharp
public string FormTitle = "Project";
```

```xml
<Form Name="MainForm" Text="{Binding FormTitle}" />
```

```csharp
FormTitle = "Project - " + project.Name;
ReloadBinding("MainForm", "Text");
```

Use the smallest useful scope:

```csharp
ReloadBinding("Status", "Text");
ReloadBindings("AccountPanel");
ReloadBindings();
```

`ReloadBindings(name)` refreshes the named object and its control subtree.
`ReloadBindings()` also refreshes registered `ItemsControl` instances. These
methods are also useful for retained function expressions and state supplied by
third-party snapshot models. Wrapping such a value in `PropertyBinding<T>` is
another option. Call explicit reload APIs on the
WinForms owner thread; reactive source notifications use the dispatcher
described above. If reevaluation fails, `ReloadBinding` and `ReloadBindings`
throw `WinFormsXamlLoadException` with the original markup source, element
path, property, line, and position, even though the XML tree is no longer being
built.

## Nested paths

Use dots to read nested public properties or fields:

```xml
<Label Text="{Binding Customer.Name}" />
<Label Text="{Binding Customer.Address.City}" />
<PictureBox Image="{Binding Customer.Avatar}"
            SizeMode="Zoom" />
```

```csharp
public Customer Customer;
```

Each dot must separate two named members. Empty segments such as
`Customer..Name`, `.Name`, or `Name.` are reported as markup errors instead of
being silently ignored. The single path `.` is intentionally different: it
binds the complete current context, which is especially useful inside an item
template.

If an intermediate value is `null`, the binding resolves to `null` rather than
throwing while walking the remaining path. A `PropertyBinding<T>` encountered
at any segment is observed; changing its `Value` re-resolves the remaining path
without a manual reload.

## Boolean negation

Put `!` inside the binding path:

```xml
<Button Text="Start"
        Enabled="{Binding !IsBusy}" />

<Label Text="No results"
       Visible="{Binding !HasResults}" />
```

The final value must be boolean-compatible.

## Comparison and logical expressions

A complete one-way `Binding` can evaluate a comparison or logical expression.
The result is a Boolean and can be used by `Condition` or by any other normal
one-way target such as `Enabled` or `Visible`. These are the requested forms;
notice that XML must escape a less-than sign and ampersands inside an attribute:

```xml
Condition="{Binding NumCount > 10}"
Condition="{Binding NumCount &lt;= 2}"
Condition="{Binding NumCount &lt; 2 &amp;&amp; NumCount > 0}"
Condition='{Binding TextContent === "Text" || TextContent == ""}'
Condition="{Binding doubleNum == 2.6}"
```

The XML parser changes `&lt;` back to `<` and `&amp;&amp;` back to `&&` before the
binding parser sees the expression. `>` and `||` need no XML escaping. When a
double-quoted XML attribute also needs a double-quoted string literal, use
`&quot;` for the inner quotes or, as above, put single quotes around the XML
attribute. `""` is an actual empty string, so the fourth condition is true when
`TextContent` is exactly `"Text"` or exactly empty. String comparisons are
ordinal and case-sensitive. `===` is a supported alias for `==`; `!==` is the
corresponding alias for `!=` and does not introduce a different coercion rule.

The supported operands are dotted binding paths (or `.` for the complete
current context), parentheses, quoted strings, invariant-culture finite
numbers including decimal and exponent notation, and the `true`, `false`, and
`null` literals. Boolean and null keywords are case-insensitive. Strings may
use single or double quotes and the `\\`, `\'`, `\"`, `\n`, `\r`, and `\t`
escapes. A leading `+` or `-` is part of a numeric literal, not an arithmetic
operator. The supported operators, from highest to lowest precedence, are:

| Precedence | Operators | Contract |
| --- | --- | --- |
| 1 | `( ... )`, paths, literals | Parentheses override normal precedence. |
| 2 | `!` | Unary logical negation; the operand must be boolean-compatible. |
| 3 | `<`, `<=`, `>`, `>=` | Both operands must be numeric. |
| 4 | `==`, `===`, `!=`, `!==` | Equality or inequality; the three-character forms are aliases. |
| 5 | `&&` | Logical AND; both operands must be boolean-compatible. |
| 6 | `||` | Logical OR; both operands must be boolean-compatible. |

Operators at the same precedence are evaluated from left to right. Comparison
evaluation does not coerce strings to numbers or booleans and does not convert
unrelated CLR types merely to make them comparable. Numeric CLR types can be
compared with one another numerically; strings compare only with strings,
Booleans with Booleans, `null` equals only `null`, and otherwise equal CLR
types use their normal equality. Relational operators are numeric only. Logical
operators intentionally retain the framework's existing boolean-compatible
conversion used by a simple `{Binding !IsBusy}`. After the complete expression
returns a Boolean, assignment to the target retains the normal one-way target
conversion rules.

This is an expression language, not embedded C#. It has no method calls,
indexers, property mutation, assignment, arithmetic, ternary operator, or
object construction. Calculate such values in code-behind and expose a public
property/field, or use a `{Function ...}` expression where a function is the
appropriate boundary. A computed binding is one-way only; adding
`Mode=TwoWay` is rejected because there is no single source endpoint to update.

All distinct operand paths are resolved before evaluation and all observable
paths participate in reactivity. Evaluation is intentionally eager: `&&` and
`||` do not suppress operand resolution or type validation. A missing path is
therefore reported even when ordinary Boolean short-circuiting would make its
value unnecessary, for example `false && Missing.Member`. A `PropertyBinding<T>` or
`INotifyPropertyChanged` dependency on any operand re-evaluates the whole
expression; snapshot fields still require the normal explicit reload.

For bounded parsing, the computed expression text is limited to 1,024
characters, 256 parsed tokens, and 32 nested parenthesis levels. Exceeding a
limit, using an unsupported token, resolving a missing operand, or comparing
incompatible values produces a load/reload error instead of running arbitrary
code or guessing a conversion.

## Structural conditions and static names

`Condition` is a structural, one-way binding:

```xml
<Panel Name="ReadyPanel"
       Condition="{Binding IsReady}">
  <Label Text="Ready" />
</Panel>
```

When a condition containing a binding, function, or preset initially resolves
to false, the runtime retains the element in a collapsed state. Changing an
observed `PropertyBinding<bool>` re-evaluates the condition automatically, so a
false-to-true change can show the existing element without `ReloadBindings`. A
snapshot field or function with no reactive path argument has no discoverable
change event; after changing that state, reload the condition explicitly:

```csharp
ReloadBinding("ReadyPanel", "Condition");
```

`Condition` and `Visibility` are independent constraints. `Visibility="Hidden"`,
`Visibility="Collapsed"`, or `Visible="false"` still hides the control when its
condition is true. When a component invocation and its template both contribute
conditions, all of them must be true. One condition never overrides another.

Because `Condition` controls structure, `Mode=TwoWay` is rejected. `Name`
defines element identity rather than mutable state; it must be a static literal
value and cannot contain a binding, function, or preset expression.

## Bind real CLR objects

A direct binding preserves the returned object. This is useful for WinForms
properties that do not accept meaningful XML strings:

```csharp
public System.Drawing.Image PreviewImage;
public System.Drawing.Icon CurrentIcon;
public System.Drawing.Color StatusColor = Color.DarkGreen;
public Padding EditorPadding = new Padding(12);
```

```xml
<Form Name="MainForm" Icon="{Binding CurrentIcon}">
  <Panel Name="Editor" Padding="{Binding EditorPadding}">
    <PictureBox Name="Preview"
                Image="{Binding PreviewImage}"
                SizeMode="Zoom" />
    <Label Name="Status"
           Text="{Binding StatusText}"
           ForeColor="{Binding StatusColor}" />
  </Panel>
</Form>
```

`Image`, `Icon`, `Font`, `Color`, `Padding`, enums, custom objects, and `null`
can all be assigned as typed values when the destination property accepts them.
These fields are snapshots; call the appropriate reload method after changing
one, or use `PropertyBinding<T>` when the value should be reactive.

## Text interpolation

Mix bindings with literal text when the destination is a string:

```xml
<Label Text="Signed in as {Binding User.DisplayName}" />
<Label Text="Page {Binding PageNumber} of {Binding PageCount}" />
```

For formatting or decisions, use a function.

## Functions

A function expression calls a method on the code-behind object:

```csharp
public string FormatStatus(string state, int count)
{
    return state + " (" + count.ToString() + ")";
}
```

```xml
<Label Name="Status"
       Text="{Function FormatStatus(State, Count)}" />
```

Functions can return typed objects too:

```csharp
public Image GetStatusImage(string state)
{
    return state == "Ready" ? _readyImage : _warningImage;
}
```

```xml
<PictureBox Name="StatusImage"
            Image="{Function GetStatusImage(State)}"
            SizeMode="CenterImage" />
```

A complete retained function expression observes each explicit binding-path
argument. In the example above, `PropertyBinding<T>` changes to `State` update
the image without a reload; `FormatStatus(State, Count)` observes both paths. Literal
arguments and the special `.`, `DataContext`, `this`, and `CodeBehind` tokens do
not identify a member to observe. A zero-argument function, or a function that
reads additional state only inside its method body, still uses the explicit
reload APIs.

Inside an `ItemsControl.ItemTemplate`, bindings with omitted `Source` use the
current item.
Functions still call the code-behind object and can receive `.` for that item:

```xml
<ItemsControl Name="Documents">
  <ItemsControl.ItemTemplate>
    <StackPanel Orientation="Horizontal">
      <PictureBox Image="{Function GetDocumentIcon(.)}"
                  SizeMode="CenterImage" />
      <Label Text="{Binding FileName}" />
    </StackPanel>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

Use a normal binding with `Source=CodeBehind` when the Form value can be exposed
directly. It avoids an unnecessary function call and remains reactive inside
item or component contexts.

## Bindings in styles

Style setters can be dynamic:

```xml
<Form.Resources>
  <Style Key="LiveStatus" TargetType="Label">
    <Setter Property="Text" Value="{Binding StatusText}" />
    <Setter Property="ForeColor" Value="{Binding StatusColor}" />
  </Style>
</Form.Resources>

<Label Name="Status" Style="LiveStatus" />
```

Switching to another style removes bindings owned by the old style. A direct
element property remains the local value and has precedence:

```xml
<Label Name="Status"
       Style="LiveStatus"
       ForeColor="DarkBlue" />
```

## Events are normal WinForms events

Event attributes call code-behind methods with the normal delegate signature:

```xml
<Button Text="Save" Click="Save_Click" />
<TextBox Name="Search" TextChanged="Search_TextChanged" />
```

```csharp
private void Save_Click(object sender, EventArgs e)
{
    Save();
}

private void Search_TextChanged(object sender, EventArgs e)
{
    TextBox search = (TextBox)sender;
    _query = search.Text;
    RunSearch();
}
```

Application subscriptions made with `+=` remain independent from handlers
registered by the XML runtime.

## Practical update pattern

For reactive state owned by an `XmlForm`, update stable wrappers:

```csharp
public readonly PropertyBinding<Project> Project =
    new PropertyBinding<Project>();

public readonly PropertyBinding<bool> IsReady =
    new PropertyBinding<bool>(false);

public readonly PropertyBinding<string> StatusText =
    new PropertyBinding<string>("Starting");

private void FinishLoading(Project project)
{
    Project.Value = project;
    IsReady.Value = true;
    StatusText.Value = "Ready";
}
```

`PropertyBinding<T>` refreshes bindings automatically and exposes a public
change event plus versioned thread-safe updates. Functions observe explicit
reactive path arguments; functions without such an argument and snapshot model
values use the explicit reload APIs. A reactive `Condition` is one-way. Dynamic
false elements remain collapsed and retained, including item-template roots, so
a source change can show them automatically.
