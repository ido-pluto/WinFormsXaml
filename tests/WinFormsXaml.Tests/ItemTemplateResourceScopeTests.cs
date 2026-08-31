using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    internal static class ItemTemplateResourceScopeTests
    {
        private sealed class Row
        {
            public string Text;

            public Row(string text)
            {
                Text = text;
            }
        }

        private sealed class ImageRow
        {
            public byte[] SourceBytes;
        }

        public static void Run()
        {
            TestTemplateStylesAreCompiledAndIsolated();
            TestNestedItemsControlRetainsTemplateStyleScope();
            TestTemplatePresetsAreImportedOnce();
            TestBindingHeavyTemplateUsesIndexedTargets();
            TestMutableByteImageSourcesRefreshInPlace();
        }

        private static void TestMutableByteImageSourcesRefreshInPlace()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <Image Name='MappedImage' " +
                "          Source='{Binding SourceBytes}' />" +
                "      <PictureBox Name='NativeImage' " +
                "          Source='{Binding SourceBytes}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";
            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ImageRow row = new ImageRow();
                row.SourceBytes = CreateBmpBytes(Color.Red);
                ItemsBinding<ImageRow> items =
                    new ItemsBinding<ImageRow>();
                items.Add(row);

                rows.CreateControl();
                rows.ItemsSource = items;

                Panel rowPanel = rows.Controls[0] as Panel;
                ImageControl mapped = rowPanel == null
                    ? null
                    : rowPanel.Controls["MappedImage"] as ImageControl;
                PictureBox native = rowPanel == null
                    ? null
                    : rowPanel.Controls["NativeImage"] as PictureBox;

                AssertTrue(
                    mapped != null && native != null,
                    "byte-image item controls are realized");

                Image initial = mapped.Image;
                AssertSame(
                    initial,
                    native.Image,
                    "item Image and PictureBox initially share one decode");

                byte[] blue = CreateBmpBytes(Color.Blue);
                AssertEqual(
                    row.SourceBytes.Length,
                    blue.Length,
                    "BMP fixtures support same-array item mutation");
                Array.Copy(blue, 0, row.SourceBytes, 0, blue.Length);

                items.ReloadItem(0);
                DrainCallbacks();

                Image blueImage = mapped.Image;
                AssertTrue(
                    !Object.ReferenceEquals(initial, blueImage),
                    "ReloadItem replaces an in-place-mutated byte image");
                AssertSame(
                    blueImage,
                    native.Image,
                    "ReloadItem shares the replacement decode across controls");
                AssertColorNear(
                    Color.Blue,
                    ((Bitmap)blueImage).GetPixel(0, 0),
                    "ReloadItem renders the mutated item bytes");
                AssertImageDisposed(
                    initial,
                    "ReloadItem retires the decode after its last owner moves");

                items.ReloadItem(0);
                DrainCallbacks();
                AssertSame(
                    blueImage,
                    mapped.Image,
                    "an unchanged ReloadItem reuses the installed decode");

                byte[] green = CreateBmpBytes(Color.Green);
                Array.Copy(green, 0, row.SourceBytes, 0, green.Length);
                items.ReloadItems();
                DrainCallbacks();

                Image greenImage = mapped.Image;
                AssertTrue(
                    !Object.ReferenceEquals(blueImage, greenImage),
                    "ReloadItems replaces an in-place-mutated byte image");
                AssertSame(
                    greenImage,
                    native.Image,
                    "ReloadItems reuses one replacement decode per source");
                AssertColorNear(
                    Color.Green,
                    ((Bitmap)greenImage).GetPixel(0, 0),
                    "ReloadItems renders the latest item bytes");
                AssertImageDisposed(
                    blueImage,
                    "ReloadItems retires the previous shared decode");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestTemplateStylesAreCompiledAndIsolated()
        {
            const string markup =
                "<Panel>" +
                "  <ItemsControl Name='Rows' Virtualizing='false' " +
                "      ProgressiveRendering='false'>" +
                "    <ItemsControl.ItemTemplate>" +
                "      <Panel>" +
                "        <Panel.Resources>" +
                "          <Style TargetType='Label'>" +
                "            <Setter Property='Foreground' Value='Red' />" +
                "          </Style>" +
                "          <Style Key='RowCaption' TargetType='Label'>" +
                "            <Setter Property='Text' Value='{Binding Text}' />" +
                "          </Style>" +
                "        </Panel.Resources>" +
                "        <Label Style='RowCaption' />" +
                "      </Panel>" +
                "    </ItemsControl.ItemTemplate>" +
                "  </ItemsControl>" +
                "</Panel>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ArrayList values = new ArrayList();
                values.Add(new Row("First"));
                values.Add(new Row("Second"));
                rows.SetItems(values);

                AssertEqual(
                    "First",
                    GetRowLabel(rows, 0).Text,
                    "first row resolves cached dynamic style setter");
                AssertEqual(
                    "Second",
                    GetRowLabel(rows, 1).Text,
                    "second row resolves the same style against its own item");
                AssertEqual(
                    Color.Red,
                    GetRowLabel(rows, 0).ForeColor,
                    "template implicit style is applied");

                IDictionary namedStyles =
                    GetField(runtime, "_namedStyles") as IDictionary;
                IList implicitStyles =
                    GetField(runtime, "_implicitStyles") as IList;

                AssertTrue(
                    namedStyles != null &&
                    !namedStyles.Contains("RowCaption"),
                    "template named style does not leak into runtime resources");
                AssertEqual(
                    0,
                    implicitStyles == null ? -1 : implicitStyles.Count,
                    "template implicit style does not accumulate globally");

                rows.ReloadItems();
                rows.ReloadItems();

                AssertEqual(
                    0,
                    implicitStyles.Count,
                    "repeated realization does not append implicit styles");

                IDictionary compiledTemplates =
                    GetField(runtime, "_compiledItemTemplates") as IDictionary;

                AssertEqual(
                    1,
                    compiledTemplates == null
                        ? -1
                        : compiledTemplates.Count,
                    "one compiled resource scope is retained per template");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestNestedItemsControlRetainsTemplateStyleScope()
        {
            const string markup =
                "<ItemsControl Name='OuterRows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <Panel.Resources>" +
                "        <Style Key='NestedRow' TargetType='Label'>" +
                "          <Setter Property='Foreground' Value='Blue' />" +
                "        </Style>" +
                "      </Panel.Resources>" +
                "      <ItemsControl Virtualizing='false' " +
                "          ProgressiveRendering='false'>" +
                "        <ItemsControl.ItemTemplate>" +
                "          <Label Style='NestedRow' Text='{Binding .}' />" +
                "        </ItemsControl.ItemTemplate>" +
                "      </ItemsControl>" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl outer =
                    runtime.GetItemsControl("OuterRows");
                ArrayList outerValues = new ArrayList();
                outerValues.Add("outer");
                outer.SetItems(outerValues);

                Panel outerRow = outer.Controls[0] as Panel;
                XamlRuntime.ItemsControl nested =
                    outerRow == null || outerRow.Controls.Count == 0
                        ? null
                        : outerRow.Controls[0] as XamlRuntime.ItemsControl;

                AssertTrue(
                    nested != null,
                    "nested ItemsControl is realized");

                ArrayList nestedValues = new ArrayList();
                nestedValues.Add("nested");
                nested.SetItems(nestedValues);

                Label nestedLabel = nested.Controls[0] as Label;

                AssertTrue(
                    nestedLabel != null,
                    "nested row is realized after the outer scope unwinds");
                AssertEqual(
                    Color.Blue,
                    nestedLabel.ForeColor,
                    "nested template retains its declaration style scope");

                IDictionary namedStyles =
                    GetField(runtime, "_namedStyles") as IDictionary;

                AssertTrue(
                    namedStyles != null &&
                    !namedStyles.Contains("NestedRow"),
                    "nested retained scope remains isolated from runtime styles");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static void TestTemplatePresetsAreImportedOnce()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <Presets Name='RowTheme' Selected='Light'>" +
                "        <Preset Name='Light'>" +
                "          <Set Key='Color' Value='Green' />" +
                "        </Preset>" +
                "      </Presets>" +
                "      <Label Text='{Binding Text}' " +
                "          Foreground='{Preset RowTheme.Color}' />" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);
            XamlRuntime.ItemsControl rows = null;

            try
            {
                rows = runtime.GetItemsControl("Rows");
                ArrayList values = new ArrayList();
                values.Add(new Row("One"));
                values.Add(new Row("Two"));
                values.Add(new Row("Three"));
                rows.SetItems(values);

                AssertEqual(
                    Color.Green,
                    GetRowLabel(rows, 2).ForeColor,
                    "compiled template preset resolves for every row");

                IDictionary loadedPresetElements =
                    GetField(runtime, "_loadedPresetElements") as IDictionary;

                AssertEqual(
                    1,
                    loadedPresetElements == null
                        ? -1
                        : loadedPresetElements.Count,
                    "one annotated preset definition is imported");

                rows.ReloadItems();
                rows.ReloadItems();

                AssertEqual(
                    1,
                    loadedPresetElements.Count,
                    "row clones do not re-import template presets");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertTrue(
                rows == null ||
                GetField(rows, "_templateContext") == null,
                "ItemsControl releases its captured template context");

            IDictionary compiledAfterDispose =
                GetField(runtime, "_compiledItemTemplates") as IDictionary;

            AssertEqual(
                0,
                compiledAfterDispose == null
                    ? 0
                    : compiledAfterDispose.Count,
                "runtime disposal releases compiled template scopes");
        }

        private static void TestBindingHeavyTemplateUsesIndexedTargets()
        {
            const string markup =
                "<ItemsControl Name='Rows' Virtualizing='false' " +
                "    ProgressiveRendering='false'>" +
                "  <ItemsControl.ItemTemplate>" +
                "    <Panel>" +
                "      <Label Text='{Binding Text}' />" +
                "      <Panel><Label Text='{Binding Text}' /></Panel>" +
                "      <Label Text='{Binding Text}' />" +
                "      <Panel><Label Text='{Binding Text}' /></Panel>" +
                "      <Label Text='{Binding Text}' />" +
                "      <Panel><Label Text='{Binding Text}' /></Panel>" +
                "      <Label Text='{Binding Text}' />" +
                "      <Panel><Label Text='{Binding Text}' /></Panel>" +
                "    </Panel>" +
                "  </ItemsControl.ItemTemplate>" +
                "</ItemsControl>";

            XamlRuntime runtime = XamlRuntime.Load(markup);

            try
            {
                XamlRuntime.ItemsControl rows =
                    runtime.GetItemsControl("Rows");
                ArrayList values = new ArrayList();
                values.Add(new Row("Indexed"));
                rows.SetItems(values);

                Panel row = rows.Controls[0] as Panel;
                int matchingLabels = CountLabelsWithText(
                    row,
                    "Indexed");

                AssertEqual(
                    8,
                    matchingLabels,
                    "binding-heavy template resolves every indexed XML target");
            }
            finally
            {
                runtime.Dispose();
            }
        }

        private static int CountLabelsWithText(
            Control root,
            string text)
        {
            if (root == null)
                return 0;

            int count = 0;
            Label label = root as Label;

            if (label != null && label.Text == text)
                count++;

            int i;

            for (i = 0; i < root.Controls.Count; i++)
            {
                count += CountLabelsWithText(
                    root.Controls[i],
                    text);
            }

            return count;
        }

        private static Label GetRowLabel(
            XamlRuntime.ItemsControl rows,
            int index)
        {
            Panel panel = rows.Controls[index] as Panel;
            Label label = panel == null || panel.Controls.Count == 0
                ? null
                : panel.Controls[0] as Label;

            if (label == null)
            {
                throw new InvalidOperationException(
                    "Expected a Label in realized row " + index + ".");
            }

            return label;
        }

        private static object GetField(object target, string name)
        {
            if (target == null)
                return null;

            Type type = target.GetType();

            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);

                if (field != null)
                    return field.GetValue(target);

                type = type.BaseType;
            }

            return null;
        }

        private static byte[] CreateBmpBytes(Color color)
        {
            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(1, 1))
            {
                bitmap.SetPixel(0, 0, color);
                bitmap.Save(stream, ImageFormat.Bmp);
                return stream.ToArray();
            }
        }

        private static void DrainCallbacks()
        {
            int i;

            for (i = 0; i < 6; i++)
                Application.DoEvents();
        }

        private static void AssertImageDisposed(
            Image image,
            string message)
        {
            bool disposed = false;

            try
            {
                int width = image.Width;

                if (width < 0)
                    disposed = true;
            }
            catch (Exception)
            {
                disposed = true;
            }

            AssertTrue(disposed, message);
        }

        private static void AssertColorNear(
            Color expected,
            Color actual,
            string message)
        {
            const int tolerance = 24;
            bool close =
                Math.Abs((int)expected.R - (int)actual.R) <= tolerance &&
                Math.Abs((int)expected.G - (int)actual.G) <= tolerance &&
                Math.Abs((int)expected.B - (int)actual.B) <= tolerance;

            if (!close)
            {
                throw new InvalidOperationException(
                    message + ": expected near " + expected +
                    ", actual " + actual + ".");
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
                throw new InvalidOperationException(message);
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": expected " + expected +
                    ", actual " + actual + ".");
            }
        }
    }
}
