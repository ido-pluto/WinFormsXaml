---
layout: home

hero:
  name: WinFormsXaml
  text: Readable XML interfaces for Windows Forms
  tagline: Build normal WinForms controls with embedded XML, reactive bindings, live presets, and high-performance virtualized lists.
  actions:
    - theme: brand
      text: Get started
      link: /guide/getting-started
    - theme: alt
      text: Browse examples
      link: /guide/sample-applications
    - theme: alt
      text: Copy a template
      link: /guide/authoring-templates

features:
  - title: Familiar WinForms API
    details: Use Form, Label, TextBox, Button, and other native control names in XML, then work with their normal WinForms types in C#.
  - title: Clean code-behind
    details: Keep each embedded XML interface beside a C# class with stable reactive bindings, simple snapshot fields, events, functions, and precise reload scopes.
  - title: Live shared presets
    details: Switch themes, density, language, dimensions, text, and typed CLR values across one form or the complete application.
  - title: Fully styleable tabs
    details: Own every TabView header and content color, border, padding, and spacing while preserving inherited LTR and RTL behavior.
  - title: Reusable components
    details: Register typed C# controls or embedded XML fragments with bindable properties and use them as elements throughout the application.
  - title: Fast repeated data
    details: Start with a simple ItemsControl and get keyed updates and progressive rendering. Opt into a bounded virtual viewport only for measured large-list workloads.
  - title: Responsive flex layout
    details: Build rows and columns with alignment, wrapping, gaps, and weighted growth while keeping ordinary WinForms controls.
---

> **Vibe-coding disclaimer:** This project is vibe coded and was developed with
> extensive AI assistance. Review and test it for your application and target
> environment before relying on it in production.

## A normal WinForms form, written in XML

```xml
<Form xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:noNamespaceSchemaLocation="../WinFormsXaml.xsd"
      Class="MyProduct.UI.MainForm"
      Name="MainForm"
      Text="Customer search"
      Width="640"
      Height="420">
  <StackPanel Margin="16">
    <TextBox Text="{Binding Query, Mode=TwoWay}" />
    <Button Text="Search" Click="Search_Click" Margin="0,8,0,8" />
    <ItemsControl ItemsSource="{Binding Results}">
      <ItemsControl.ItemTemplate>
        <Label Text="{Binding Title}" AutoSize="true" />
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </StackPanel>
</Form>
```

```csharp
using System;
using System.Windows.Forms;
using WinFormsXaml;

namespace MyProduct.UI
{
    public sealed class MainForm : XmlForm
    {
        public MainForm()
            : base("MainForm.xml")
        {
        }
    }
}

namespace MyProduct
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new UI.MainForm().Start();
        }
    }
}
```
