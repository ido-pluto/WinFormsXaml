# Styleable tabs with TabView

`TabView` is an adaptive tab control. With its appearance properties at their
defaults it hosts native `System.Windows.Forms.TabControl` chrome. Applying an
effective custom appearance switches the same `TabViewItem` pages to the
framework-painted surface, where every header, border, content color, spacing,
and selected state can be styled without an application `DrawItem` handler.

The switch preserves the page objects, handles, selection, bindings, and focused
descendant. Set `ForceNativeTabs="true"` when operating-system chrome must win
even while custom appearance values are stored. Native `TabControl` and
`TabPage` remain available for applications that do not need the adaptive API.

## Declare pages

Place `TabViewItem` elements directly under `TabView`. Each item has a `Header`
and zero or one visual content child:

```xml
<TabView Name="WorkspaceTabs"
         SelectedIndex="0"
         Dock="Fill">
  <TabViewItem Header="Overview">
    <Grid>
      <Label Text="Account overview" />
    </Grid>
  </TabViewItem>

  <TabViewItem Header="Activity">
    <ItemsControl ItemsSource="{Binding ActivityRows}">
      <ItemsControl.ItemTemplate>
        <Label Text="{Binding Summary}" />
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </TabViewItem>
</TabView>
```

The explicit collection form is equivalent:

```xml
<TabView SelectedIndex="1">
  <TabView.TabItems>
    <TabViewItem Header="General">
      <Panel />
    </TabViewItem>
    <TabViewItem Header="Advanced">
      <Panel />
    </TabViewItem>
  </TabView.TabItems>
</TabView>
```

Do not mix unrelated direct controls into a `TabView`. A visual page must be
inside a `TabViewItem`, and a `TabViewItem` rejects a second visual content
child. `TabView.Resources` and `TabViewItem.Resources` are metadata scopes and
do not count as visual content.

`SelectedIndex` may appear before the item declarations in XML. WinFormsXaml
defers that initial selection until all declared items are attached. `-1`
means that no item is selected.

## Style every part

The style properties use normal `System.Drawing.Color`,
`System.Windows.Forms.Padding`, and `Int32` values:

| Property | Type | Default | Controls |
| --- | --- | --- | --- |
| `TabBackground` | `Color` | `SystemColors.Control` | Unselected header fill. |
| `SelectedTabBackground` | `Color` | `SystemColors.Window` | Selected header fill. |
| `TabForeground` | `Color` | `SystemColors.ControlText` | Unselected header text. |
| `SelectedTabForeground` | `Color` | `SystemColors.ControlText` | Selected header text. |
| `TabBorderBrush` | `Color` | `SystemColors.ControlDark` | Header border color. |
| `TabBorderThickness` | `Padding` | `1` | Per-edge header border thickness. |
| `TabPadding` | `Padding` | `8,4,8,4` | Space between header text and its border. |
| `HeaderSpacing` | nonnegative `int` | `0` | Space between adjacent headers. |
| `ContentBackground` | `Color` | `SystemColors.Window` | Selected-page surface fill. |
| `ContentBorderBrush` | `Color` | `SystemColors.ControlDark` | Content frame color. |
| `ContentBorderThickness` | `Padding` | `1` | Per-edge content frame thickness. |
| `ContentPadding` | `Padding` | `0` | Space between the frame and selected page. |
| `TabCornerRadius` | nonnegative `int` | `0` | Framework-painted header corner radius. |
| `SelectedTabCornerRadius` | `int` ≥ `-1` | `-1` | Selected radius; `-1` inherits `TabCornerRadius`. |

`ForceNativeTabs` is a Boolean behavior property rather than an appearance
value. Any nondefault effective appearance selects framework rendering unless
it is true. Returning every appearance property to its default restores native
tabs automatically. Radius values are stored but ignored while native tabs are
active.

All of these properties can be literal values, bindings, functions, preset
expressions, or style setters. A four-part `Padding` value always means
physical `left,top,right,bottom`; it is not reversed in RTL.

Each appearance property raises its matching change event, including
`TabBackgroundChanged`, `SelectedTabBackgroundChanged`,
`TabForegroundChanged`, `SelectedTabForegroundChanged`,
`TabBorderBrushChanged`, `TabBorderThicknessChanged`, `TabPaddingChanged`,
`HeaderSpacingChanged`, `ContentBackgroundChanged`,
`ContentBorderBrushChanged`, `ContentBorderThicknessChanged`, and
`ContentPaddingChanged`, plus `TabCornerRadiusChanged` and
`SelectedTabCornerRadiusChanged`. `ForceNativeTabsChanged` reports forced-mode
changes. These are normal XML event attributes and are included in the packaged
XSD IntelliSense contract.

```xml
<TabView TabBackground="{Preset Theme.TabSurface}"
         SelectedTabBackground="{Preset Theme.TabSelected}"
         TabForeground="{Preset Theme.TextMuted}"
         SelectedTabForeground="{Preset Theme.Text}"
         TabBorderBrush="{Preset Theme.Border}"
         TabBorderThickness="1"
         TabPadding="12,7"
         HeaderSpacing="4"
         TabCornerRadius="8"
         SelectedTabCornerRadius="10"
         ContentBackground="{Preset Theme.Surface}"
         ContentBorderBrush="{Preset Theme.Border}"
         ContentBorderThickness="1"
         ContentPadding="16">
  <TabViewItem Header="Details">
    <Panel />
  </TabViewItem>
</TabView>
```

The same contract works in a reusable style:

```xml
<TabView Name="StyledTabs" Style="DarkTabs">
  <TabView.Resources>
    <Style Key="DarkTabs" TargetType="TabView">
      <Setter Property="TabBackground" Value="#20242B" />
      <Setter Property="SelectedTabBackground" Value="#303640" />
      <Setter Property="TabForeground" Value="#AEB6C2" />
      <Setter Property="SelectedTabForeground" Value="White" />
      <Setter Property="TabBorderBrush" Value="#49515E" />
      <Setter Property="TabBorderThickness" Value="1" />
      <Setter Property="TabPadding" Value="12,7" />
      <Setter Property="HeaderSpacing" Value="3" />
      <Setter Property="ContentBackground" Value="#181B20" />
      <Setter Property="ContentBorderBrush" Value="#49515E" />
      <Setter Property="ContentBorderThickness" Value="1" />
      <Setter Property="ContentPadding" Value="12" />
    </Style>
  </TabView.Resources>

  <TabViewItem Header="First">
    <Panel />
  </TabViewItem>
</TabView>
```

`BackColor` fills any unused outer/header area. `ContentBackground` owns the
selected-page surface inside the content frame.

To keep native chrome for one preset while retaining custom values for another,
bind the behavior property:

```xml
<TabView ForceNativeTabs="{Preset Theme == System}"
         TabBackground="{Preset Theme.TabSurface}"
         SelectedTabBackground="{Preset Theme.TabSelected}" />
```

`ForceNativeTabs`, both radius properties, and every other normal property are
reactive when their expression source is observable.

## Observe and bind selection

`TabView` exposes `SelectedIndex`, `SelectedItem`, and three events:

- `SelectedIndexChanged` when the selected index changes;
- `SelectedItemChanged` when the selected item reference changes;
- `SelectionChanged`, with `TabViewSelectionChangedEventArgs`, for one
  consolidated old/new selection notification.

`SelectedIndex` and `SelectedItem` support normal one-way and two-way bindings.
For example:

```xml
<TabView Name="WorkspaceTabs"
         SelectedIndex="{Binding ActiveTab, Mode=TwoWay}"
         SelectionChanged="WorkspaceTabs_SelectionChanged">
  <TabViewItem Header="Overview">
    <Panel />
  </TabViewItem>
  <TabViewItem Header="History">
    <Panel />
  </TabViewItem>
</TabView>
```

```csharp
public readonly PropertyBinding<int> ActiveTab =
    new PropertyBinding<int>(0);

private void WorkspaceTabs_SelectionChanged(
    object sender,
    TabViewSelectionChangedEventArgs e)
{
    // e carries the old and new selection.
}
```

Imperative code uses the read-only `TabItems` collection. The collection
supports indexing, add, insert, remove, remove-at, clear, containment/index
queries, and moving an item when its `Move` method is used. An item cannot be
`null`, appear twice in one view, or belong to two views at once.

```csharp
TabView tabs = Get<TabView>("WorkspaceTabs");
TabViewItem diagnostics = new TabViewItem();
diagnostics.Header = "Diagnostics";
diagnostics.Controls.Add(new Panel());

tabs.TabItems.Add(diagnostics);
tabs.SelectedItem = diagnostics;
tabs.TabItems.Move(tabs.TabItems.IndexOf(diagnostics), 0);
```

Only the selected page is made visible by the owner. That temporary owner hide
does not overwrite the item's requested `Visible`/`Condition` state. Hiding the
selected item selects the nearest requested-visible page; making the old page
visible again only restores its eligibility and does not steal selection.

## LTR and RTL contract

There is deliberately no TabView-specific direction property. Use the same
`FlowDirection` or native `RightToLeft` properties as the surrounding form and
components:

```xml
<Form FlowDirection="RightToLeft">
  <TabView>
    <TabViewItem Header="ראשון"><Panel /></TabViewItem>
    <TabViewItem Header="שני"><Panel /></TabViewItem>
  </TabView>
</Form>
```

Direction follows the effective inherited value. Therefore a `TabView` works
the same way when it is directly on a form, nested in layout containers, or
inside a registered C# or XML component:

- in LTR, logical item `0` is the leftmost header;
- in RTL, logical item `0` is the rightmost header;
- `TabItems` and `SelectedIndex` always keep logical declaration/collection
  order; RTL changes only visual placement and direction-sensitive navigation;
- changing a bound `FlowDirection`/`RightToLeft` value and reloading it lays out
  the existing headers again without rebuilding or reversing the collection;
- `HeaderSpacing`, padding, borders, selection, content stretch, and hit testing
  use the same effective direction.

Left and Right choose the physically adjacent enabled, requested-visible
header. Home and End select the logical first and last eligible item. `Ctrl+Tab` and
`Ctrl+Shift+Tab` move logically forward and backward in both directions. This
keeps keyboard behavior predictable while matching the visible RTL order.
