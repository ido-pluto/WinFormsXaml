# XML IntelliSense with the XSD schema

WinFormsXaml ships a hand-maintained XSD 1.0 schema. It gives Visual Studio's
built-in XML editor element and attribute completion, enum-value suggestions,
and hover documentation without a Visual Studio extension.

The schema covers:

- common native Windows Forms controls and properties, including `DataGrid`,
  `BindingNavigator`, LinkLabel appearance, and ToolStrip status/progress items;
- common control, form, input, selection, list/tree, data-grid, ToolStrip,
  browser, validation, keyboard, pointer, drag/drop, and layout events; event
  attribute values are code-behind method names;
- collection-backed .NET 2 elements such as `TreeNode`, ListView items, groups,
  and columns, plus built-in `DataGridView` columns, rows, cells, and styles;
- curated property-element completion for native collections such as
  `ListView.Columns`, `TreeView.Nodes`, `DataGridView.Columns`,
  `ToolStrip.Items`, `TabControl.TabPages`, and typed table-layout row/column
  styles;
- `Grid`, `StackPanel`, `DockPanel`, `Canvas`, flex, border, scrolling, and
  attached layout properties including integer `Panel.ZIndex`, typed row versus
  column definitions, `Auto`, fractional values, and `px`/`pt` dimensions;
- the runtime-owned `ToolTip` mapping;
- `ItemsControl`, its direct-root `ItemsControl.ItemTemplate`, active-axis
  `ItemsControl.VerticalScrollStyle`/`ItemsControl.HorizontalScrollStyle`, and
  virtualization options;
- `TabView`, direct or `TabView.TabItems` pages, selection events, complete
  header/content styling, and local resources on both tab types;
- styles, setters, local resources on common native/custom owners, inline or
  external presets, standalone/directive `Includes` with one-way `Condition`,
  and XML components;
- binding, function, and preset expression forms, plus static-resource style
  references in the contexts that accept them.

Common native enum properties provide their CLR values directly, including
Form state and border options, button dialog results, text and image alignment,
list selection/view modes, date formats, and progress fallback modes.

## Built-in completion inventory

The shipped schema declares the canonical WinFormsXaml elements directly:

```text
Grid  StackPanel  DockPanel  Canvas  FlexPanel
Border  ScrollViewer  Viewbox  TabView  TabViewItem  ItemsControl  Includes
ProgressBar  HyperlinkLabel  Image
VerticalScrollBar  HorizontalScrollBar  ScrollBarStyle
```

It also declares the common .NET 2 Windows Forms surface so Visual Studio can
offer element completion before reflection runs:

```text
Control  ContainerControl  ScrollableControl
Form  UserControl  MdiClient  Panel  FlowLayoutPanel  TableLayoutPanel
GroupBox  SplitContainer  Splitter

Label  LinkLabel  TextBox  MaskedTextBox  RichTextBox
Button  CheckBox  RadioButton
ComboBox  ListBox  CheckedListBox  ListView  TreeView
PictureBox  TrackBar  NumericUpDown  DomainUpDown
DateTimePicker  MonthCalendar  HScrollBar  VScrollBar

TabControl  TabPage  PropertyGrid
DataGrid  DataGridTextBox  DataGridView
DataGridViewComboBoxEditingControl  DataGridViewTextBoxEditingControl
MenuStrip  ContextMenuStrip  ToolStrip  StatusStrip
BindingNavigator  ToolStripContainer  ToolStripContentPanel  ToolStripPanel
MainMenu  ContextMenu  ToolBar  StatusBar
WebBrowser  PrintPreviewControl  PrintPreviewDialog
```

This inventory covers every public, concrete .NET Framework 2.0 `Control` with
a public parameterless constructor. The matching item, node, column, row, cell,
header-cell, style, menu-item, ToolStrip-item, ToolBar-button, StatusBar-panel,
and ListView subitem object elements are declared as well. The runtime can
resolve additional public Windows Forms types and registered application types
even when a static schema cannot list them.

`Image` and `PictureBox` are both global completion entries. `Image` suggests
the WPF-style `Source`/`Stretch` pair and its change events while retaining the
native PictureBox API. `PictureBox` suggests the native `Image`,
`ImageLocation`, `InitialImage`, `ErrorImage`, `WaitOnLoad`, `SizeMode`, and
image-loading events. Their boolean and enum suggestions remain unions with
Binding, Function, and Preset expressions rather than becoming literal-only.

`TabView` completion exposes its entire framework-painted surface. Color and
Padding values remain expression-capable strings, while `HeaderSpacing` offers
nonnegative integer validation and still accepts Binding, Function, or Preset
expressions:

```xml
<TabView Name="EditorTabs"
         SelectedIndex="{Binding ActiveTab, Mode=TwoWay}"
         ForceNativeTabs="{Preset Theme == System}"
         TabBackground="{Preset Theme.TabSurface}"
         SelectedTabBackground="{Preset Theme.TabSelected}"
         TabForeground="{Preset Theme.TextMuted}"
         SelectedTabForeground="{Preset Theme.Text}"
         TabBorderBrush="{Preset Theme.Border}"
         TabBorderThickness="1"
         TabPadding="10,6"
         HeaderSpacing="{Preset Theme.TabSpacing}"
         TabCornerRadius="8"
         SelectedTabCornerRadius="10"
         ContentBackground="{Preset Theme.Surface}"
         ContentBorderBrush="{Preset Theme.Border}"
         ContentBorderThickness="1"
         ContentPadding="12"
         SelectedIndexChanged="Tabs_SelectedIndexChanged"
         SelectedItemChanged="Tabs_SelectedItemChanged"
         SelectionChanged="Tabs_SelectionChanged">
  <TabView.Resources>
    <Style TargetType="TabViewItem">
      <Setter Property="Foreground" Value="{Preset Theme.Text}" />
    </Style>
  </TabView.Resources>

  <TabView.TabItems>
    <TabViewItem Header="Document">
      <TabViewItem.Resources>
        <Style TargetType="Label">
          <Setter Property="Foreground" Value="{Preset Theme.Text}" />
        </Style>
      </TabViewItem.Resources>
      <Label Text="Document settings" />
    </TabViewItem>
    <TabViewItem Header="Preview">
      <Panel />
    </TabViewItem>
  </TabView.TabItems>
</TabView>
```

The schema deliberately does not offer `TabLayoutDirection`. Header direction
uses the common `FlowDirection` or `RightToLeft` completion. Effective
direction can be inherited from a form or registered component, and live
direction bindings relayout headers while logical `TabItems` order stays
unchanged.

`ForceNativeTabs` accepts a Boolean literal or expression. `TabCornerRadius`
accepts zero or a positive integer, and `SelectedTabCornerRadius` also accepts
`-1` to inherit the normal radius. Their matching change events are included in
completion.

Standalone `VerticalScrollBar` and `HorizontalScrollBar` completion exposes their
native-style range and event surface plus `TrackColor`, `ThumbColor`,
`ThumbHoverColor`, `ThumbPressedColor`, `ArrowColor`, `ArrowHoverColor`,
`BorderColor`, `Thickness`, and `MinimumThumbLength`. The dotted
`VerticalScrollBar.Style` and `HorizontalScrollBar.Style` property elements
complete one nested `ScrollBarStyle` object:

```xml
<VerticalScrollBar Maximum="1000"
                   LargeChange="100"
                   Value="{Binding Offset, Mode=TwoWay}">
  <VerticalScrollBar.Style>
    <ScrollBarStyle TrackColor="{Preset Theme.ScrollTrack}"
                    ThumbColor="{Preset Theme.ScrollThumb}"
                    Thickness="14" />
  </VerticalScrollBar.Style>
</VerticalScrollBar>
```

`ItemsControl` exposes two separate nullable style properties. IntelliSense
offers both matching property elements, each with exactly one nested
`ScrollBarStyle`:

```xml
<ItemsControl Orientation="Vertical"
              ItemsSource="{Binding Results}"
              ScrollBarGap="8">
  <ItemsControl.VerticalScrollStyle>
    <ScrollBarStyle TrackColor="{Preset Theme.ScrollTrack}"
                    ThumbColor="{Preset Theme.ScrollThumb}"
                    ThumbHoverColor="{Preset Theme.ScrollThumbHover}"
                    BorderColor="#303640"
                    Thickness="14"
                    MinimumThumbLength="10" />
  </ItemsControl.VerticalScrollStyle>
  <ItemsControl.ItemTemplate>
    <Label Text="{Binding Title}" />
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

The dotted scrollbar-style property elements also complete a one-way
`Condition` attribute. False or unresolved restores native chrome. IntelliSense
does not offer `Condition` directly on `ScrollBarStyle`, because the condition
controls whether the containing property is assigned:

```xml
<ItemsControl.VerticalScrollStyle Condition="{Binding UseCustomScrollBar}">
  <ScrollBarStyle Thickness="14" />
</ItemsControl.VerticalScrollStyle>
```

Use `ItemsControl.HorizontalScrollStyle` for a horizontal host. The
`VerticalScrollStyle` and `HorizontalScrollStyle` attributes accept complete
Binding, Function, or Preset expressions whose result is a `ScrollBarStyle` or
null. They intentionally do not suggest a string literal for an object. Omitted
or null preserves the native scrollbar, and only the property matching
`Orientation` is active.

`ScrollBarGap` is a non-negative `ItemsControl` metric, defaulting to zero, that
separates either native or custom bar from the items. IntelliSense suggests it
on `ItemsControl`, not on the reusable scrollbar style object.

All style attributes remain expression-capable. Positive metric literals reject
zero, while Binding, Function, and Preset expressions remain valid. The native
`HScrollBar` and `VScrollBar` entries stay available when system-rendered chrome
is desired.

Collection-backed children use the same direct nesting that the runtime
consumes:

```xml
<StackPanel Orientation="Vertical">
  <BindingNavigator>
    <ToolStripStatusLabel Text="Ready" Spring="True" />
    <ToolStripProgressBar Minimum="0" Maximum="100" Value="20" />
  </BindingNavigator>

  <ListView View="Details">
    <ColumnHeader Text="Customer" Width="180" />
    <ListViewItem Text="Ada Lovelace" />
  </ListView>

  <DataGridView AutoGenerateColumns="false"
                SelectionMode="FullRowSelect">
    <DataGridViewTextBoxColumn HeaderText="Customer"
                               DataPropertyName="Name"
                               AutoSizeMode="Fill" />
    <DataGridViewCheckBoxColumn HeaderText="Active"
                                DataPropertyName="IsActive" />
  </DataGridView>
</StackPanel>
```

`HyperlinkLabel` is the built-in navigable LinkLabel. `NavigateUri` can be a
literal or a Binding, Function, or Preset expression, while native properties
such as `LinkArea`, `LinkBehavior`, `LinkColor`, and `VisitedLinkColor` retain
their normal LinkLabel meaning. `Text` and the `Content` convenience mapping
both set the visible label:

```xml
<HyperlinkLabel Text="Open documentation"
                NavigateUri="{Binding DocumentationUri}"
                LinkBehavior="HoverUnderline" />
```

`LinkClicked` remains the native LinkLabel event. `RequestNavigate` is raised
after it and can set `HyperlinkNavigateEventArgs.Handled` to `true` when the
application wants to replace default-browser navigation.

The schema also completes native multi-link markup through
`LinkLabel.Links` and `Link` entries (`Start`, `Length`, `LinkData`, `Enabled`,
and `Visited`).

Expression hover documentation includes bare and path-named bindings,
one-way/two-way mode, negation, `Source=Current`, `Source=CodeBehind`, bare and
argument-bearing functions, preset keys, interpolation, and the safe one-way
comparison/logical grammar. Computed bindings accept paths, literals,
parentheses, `!`, relational/equality operators, `&&`, and `||`; the hover also
calls out eager operand resolution, `Mode=TwoWay` rejection, and the bounded
parser limits. In an XML attribute, `<` must be written as `&lt;` and `&&` as
`&amp;&amp;`. `Source=Current`
keeps the current context in form markup, an item template, or an XML component;
`Source=CodeBehind` selects the original code-behind/event-target object from
nested markup. XSD 1.0 treats a complete markup expression as a string, so it
cannot offer token-by-token completion inside `{Binding ...}` or function
argument lists.

Native CLR names keep their native meaning in the schema. In particular,
`ProgressBar.Style` suggests `Blocks`, `Continuous`, and `Marquee`; use
`ResourceStyle` when the same control also selects a keyed markup style.
`MarqueeAnimationSpeed` is non-negative, and
`PreferMarqueeFallback` is Boolean. The canonical `<ProgressBar>` element
selects native marquee or the native Blocks grow/drain fallback automatically;
setting that Boolean to true forces the fallback. Application XML and normal
control lookup do not name an implementation type.

Typed property completion does not make those properties literal-only. The
schema combines each normal boolean, number, dimension, and enum vocabulary
with runtime expression strings. For example, `CheckBox.Checked` suggests
`true` and `false` while also accepting
`{Binding Accepted, Mode=TwoWay}`; enum properties keep suggestions such as
`Left`, `Right`, or `Marquee` and accept Binding, Function, and Preset
expressions. Static identity and grammar metadata such as `Name`, `Class`,
component-property `Type`, include `Source`/`SourceKind`/`Assembly`, and
grid-definition structure remain intentionally strict because changing them
after construction is not a property binding.

## Associate a form with the schema

PackageReference exposes the packaged schema as a linked `WinFormsXaml.xsd`
file at the project root. Add the standard XML Schema Instance attributes to
the document root and write the schema path relative to that XML document.

For this common layout:

```text
Example/
  WinFormsXaml.xsd  (linked from the NuGet package)
  UI/
    MainForm.xml
```

`UI/MainForm.xml` uses `../WinFormsXaml.xsd`:

```xml
<Form xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
      Class="Example.UI.MainForm"
      Name="MainForm"
      Text="Schema example">
  <StackPanel Orientation="Vertical" Margin="12">
    <TextBox Text="{Binding Query, Mode=TwoWay}" />
    <Button Text="Search"
            ToolTip="Run the current query"
            Click="Search_Click" />
  </StackPanel>
</Form>
```

Visual Studio resolves `xsi:noNamespaceSchemaLocation` as a file path relative
to the XML file. It does not resolve a NuGet package ID or expand an MSBuild
property in this attribute. Use `WinFormsXaml.xsd` when both files are in the
same directory, `../WinFormsXaml.xsd` from `UI`, and
`../../WinFormsXaml.xsd` from `UI/Components`.

The same association works for component and standalone preset documents. The
following examples assume those files are also under `UI`:

```xml
<Component xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
           xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
           Class="Example.UI.Components.SearchCard">
  <Component.Properties>
    <Property Name="Title" Type="String" Required="true" />
  </Component.Properties>
  <Border Padding="12" BorderThickness="1">
    <StackPanel>
      <Label Text="{Binding Title}" />
      <Children />
    </StackPanel>
  </Border>
</Component>
```

`Class` is optional. When present, it names the public, concrete component
code-behind type created once per invocation. `<Children />` is the one empty
projection slot; a registered short-name invocation can place zero or more
visual controls there. See [Reusable components](./components) for registration,
declared properties, code-behind, and `ChildrenBind`.

```xml
<Presets xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
         Name="Theme"
         Selected="Light">
  <Preset Name="Light">
    <Set Key="Surface" Value="White" />
  </Preset>
</Presets>
```

The runtime ignores attributes in the standard XML Schema Instance namespace;
the association is editor metadata and does not become a control property.

## Find the packaged schema

The NuGet package includes the same schema in three conventional locations:

```text
content/WinFormsXaml.xsd
contentFiles/any/any/WinFormsXaml.xsd
schemas/WinFormsXaml.xsd
```

`content` supports classic `packages.config` projects, while `contentFiles`
supports PackageReference-aware clients. The `schemas` copy is a stable package
location for tools and for copying into a project. PackageReference exposes the
content-file copy as a linked, root-level `None` item named
`WinFormsXaml.xsd`; it is not copied beside the executable or into publish
output. A form in a subdirectory must therefore walk back to the project-root
link, as in `../WinFormsXaml.xsd` for `UI/MainForm.xml`.

If a Visual Studio/project-system combination does not add package content to
the project automatically, copy `schemas/WinFormsXaml.xsd` from the installed
package to the project and associate it as shown above. You can also open
**XML > Schemas**, choose **Add**, select the file, and set **Use** to enable it
for the current editor. The standard `xsi:noNamespaceSchemaLocation`
association is more portable because it travels with the XML file.

## Registered controls and reflection-based properties

The runtime accepts more markup than any static schema can know. It can discover
public WinForms types, registered controls, writable CLR properties, and events
at run time. The XSD therefore uses `processContents="lax"` wildcards:

- an unknown registered element or property is not rejected merely because it
  is absent from the shipped schema;
- known elements and attributes still receive completion and documentation;
- common dotted collection, scalar, and `Resources` property elements are
  declared, while application-specific owner/property pairs remain lax;
- XSD 1.0 cannot infer a registered C# type's custom properties or a binding
  path from the application's assemblies;
- the common control attribute list is intentionally a useful superset, while
  element-specific declarations narrow incompatible properties such as
  `DateTimePicker.Format`, range `Minimum`/`Maximum`, and the two native
  `AutoSizeMode` enum families; the runtime still rejects a property used on
  the wrong control;
- the same lax wildcard cannot safely blacklist an unknown same-namespace name,
  so the runtime remains the authoritative rejection gate for non-canonical
  package grammar.

For completion of an application-specific element, copy the schema into the
project and add a global declaration near the other control elements:

```xml
<xs:element name="StatusBadge" type="ControlType" />
```

That provides the common control suggestions while leaving custom attributes
open. Custom attribute names can be added to the local `ControlAttributes`
group when application-specific completion is worth maintaining. This remains
a normal XSD workflow and does not require an editor extension.

The schema is an authoring aid, not the runtime contract. The loader remains
the final authority for type conversion, constructor selection, component
properties, binding paths, and event-handler signatures.
