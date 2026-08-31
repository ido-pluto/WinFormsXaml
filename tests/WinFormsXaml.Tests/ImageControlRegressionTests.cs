using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using WinFormsXaml;

namespace WinFormsXaml.Tests
{
    public sealed class RetainingSourceImageControl : ImageControl
    {
        private string _probe;
        private EventHandler _probeChanged;

        public static bool ThrowOnProbeChangedRemove;
        public int ProbeChangedRemoveAttemptCount;

        public string Probe
        {
            get { return _probe; }
            set
            {
                if (String.Equals(_probe, value, StringComparison.Ordinal))
                    return;

                _probe = value;
                EventHandler handler = _probeChanged;

                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public event EventHandler ProbeChanged
        {
            add { _probeChanged += value; }
            remove
            {
                ProbeChangedRemoveAttemptCount++;

                if (ThrowOnProbeChangedRemove)
                {
                    throw new InvalidOperationException(
                        "ProbeChanged remove failed before detaching its handler.");
                }

                _probeChanged -= value;
            }
        }
    }

    internal sealed class ImageControlPaintProbe : ImageControl
    {
        public int BackgroundPaintCount;

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            BackgroundPaintCount++;
            base.OnPaintBackground(e);
        }
    }

    internal sealed class ImageControlBindingState
    {
        public byte[] SourceBytes;
        public string SourcePath;
        public object TransactionSource;
        public string CurrentStyle;
    }

    internal sealed class ImageControlOwnershipState
    {
        public readonly PropertyBinding<object> SourceValue;
        public readonly PropertyBinding<string> ProbeValue;

        public ImageControlOwnershipState(object sourceValue)
        {
            SourceValue = new PropertyBinding<object>(sourceValue);
            ProbeValue = new PropertyBinding<string>("ready");
        }
    }

    internal static class ImageControlRegressionTests
    {
        public static void Run()
        {
            using (XamlRuntime textOnlyRuntime =
                XamlRuntime.Load("<Label Text='No image' />"))
            {
                AssertEqual(
                    null,
                    GetRuntimeField(
                        textOnlyRuntime,
                        "_decodedImageCache"),
                    "a runtime that never decodes an image allocates no image cache");
            }

            ImageControlBindingState state =
                new ImageControlBindingState();

            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(2, 1))
            {
                bitmap.SetPixel(0, 0, Color.Red);
                bitmap.SetPixel(1, 0, Color.Blue);
                bitmap.Save(stream, ImageFormat.Png);
                state.SourceBytes = stream.ToArray();
            }

            state.CurrentStyle = "FillImageStyle";

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                " <Panel.Resources>" +
                "  <Style Key='FillImageStyle' TargetType='Image'>" +
                "   <Setter Property='Stretch' Value='Fill' />" +
                "  </Style>" +
                "  <Style Key='PlainImageStyle' TargetType='Image'>" +
                "   <Setter Property='Width' Value='24' />" +
                "  </Style>" +
                " </Panel.Resources>" +
                " <Image Name='DefaultImage' " +
                "   Source='{Binding SourceBytes}' />" +
                " <Image Name='NoStretchImage' " +
                "   Source='{Binding SourceBytes}' Stretch='None' />" +
                " <Image Name='FillImage' " +
                "   Source='{Binding SourceBytes}' Stretch='Fill' />" +
                " <Image Name='UniformImage' " +
                "   Source='{Binding SourceBytes}' Stretch='Uniform' />" +
                " <Image Name='UniformFillImage' " +
                "   Source='{Binding SourceBytes}' Stretch='UniformToFill' />" +
                " <PictureBox Name='NativePicture' " +
                "   Source='{Binding SourceBytes}' />" +
                " <Image Name='StyledImage' " +
                "   Style='{Binding CurrentStyle}' />" +
                "</Panel>",
                state);

            try
            {
                ImageControl defaultImage =
                    runtime.Get<ImageControl>("DefaultImage");
                ImageControl noStretch =
                    runtime.Get<ImageControl>("NoStretchImage");
                ImageControl fill =
                    runtime.Get<ImageControl>("FillImage");
                ImageControl uniform =
                    runtime.Get<ImageControl>("UniformImage");
                ImageControl uniformFill =
                    runtime.Get<ImageControl>("UniformFillImage");
                PictureBox native =
                    runtime.Get<PictureBox>("NativePicture");
                ImageControl styled =
                    runtime.Get<ImageControl>("StyledImage");

                AssertTrue(
                    defaultImage.GetType() == typeof(ImageControl),
                    "Image XML resolves to the public ImageControl type");
                AssertTrue(
                    defaultImage is PictureBox,
                    "ImageControl retains the complete PictureBox API");
                AssertTrue(
                    !typeof(ImageControl).IsSealed,
                    "ImageControl remains extensible");
                AssertTrue(
                    native.GetType() == typeof(PictureBox),
                    "PictureBox XML remains the native WinForms type");

                AssertEqual(
                    PictureBoxSizeMode.Zoom,
                    defaultImage.SizeMode,
                    "Image defaults to WPF Uniform stretch");
                AssertEqual(
                    ImageStretch.Uniform,
                    defaultImage.Stretch,
                    "Image exposes its WPF-style Uniform default");
                AssertEqual(
                    PictureBoxSizeMode.CenterImage,
                    noStretch.SizeMode,
                    "Image Stretch=None keeps the source centered");
                AssertEqual(
                    ImageStretch.None,
                    noStretch.Stretch,
                    "Image reports Stretch=None");
                AssertEqual(
                    PictureBoxSizeMode.StretchImage,
                    fill.SizeMode,
                    "Image Stretch=Fill uses native stretching");
                AssertEqual(
                    PictureBoxSizeMode.Zoom,
                    uniform.SizeMode,
                    "Image Stretch=Uniform preserves aspect ratio");
                AssertEqual(
                    PictureBoxSizeMode.Zoom,
                    uniformFill.SizeMode,
                    "Image UniformToFill keeps the native loader and animation path");
                AssertEqual(
                    ImageStretch.UniformToFill,
                    uniformFill.Stretch,
                    "Image retains the requested UniformToFill value");
                AssertEqual(
                    PictureBoxSizeMode.Normal,
                    native.SizeMode,
                    "native PictureBox keeps its native default");
                AssertEqual(
                    ImageStretch.Fill,
                    styled.Stretch,
                    "Image Stretch applies through a markup style");

                state.CurrentStyle = "PlainImageStyle";
                runtime.ReloadBinding("StyledImage", "Style");

                AssertEqual(
                    ImageStretch.Uniform,
                    styled.Stretch,
                    "style replacement restores the Image Uniform baseline");
                AssertEqual(
                    PictureBoxSizeMode.Zoom,
                    styled.SizeMode,
                    "restored Image stretch keeps native SizeMode coherent");

                Image sharedImage = defaultImage.Image;

                AssertTrue(
                    sharedImage != null && sharedImage.Width == 2,
                    "Image Source accepts bound encoded bytes");
                AssertSame(
                    sharedImage,
                    noStretch.Image,
                    "Image controls share the decoded byte source");
                AssertSame(
                    sharedImage,
                    fill.Image,
                    "Stretch does not create a resized bitmap copy");
                AssertSame(
                    sharedImage,
                    uniform.Image,
                    "Uniform stretch reuses the decoded bitmap");
                AssertSame(
                    sharedImage,
                    uniformFill.Image,
                    "UniformToFill reuses the decoded bitmap");
                AssertSame(
                    sharedImage,
                    native.Image,
                    "Image and PictureBox share one optimized source pipeline");
                AssertSame(
                    sharedImage,
                    defaultImage.Source,
                    "Source exposes the installed native PictureBox image");

                int mappedSourceChanged = 0;
                defaultImage.SourceChanged +=
                    delegate(object sender, EventArgs e)
                    {
                        mappedSourceChanged++;
                    };
                state.SourceBytes = CreatePngBytes(3, 1, Color.Green);
                runtime.ReloadBinding("DefaultImage", "Source");

                AssertEqual(
                    1,
                    mappedSourceChanged,
                    "mapped Source updates raise SourceChanged exactly once");
                AssertEqual(
                    3,
                    defaultImage.Source.Width,
                    "mapped SourceChanged observes the installed image");

                runtime.ReloadBinding("DefaultImage", "Source");

                AssertEqual(
                    1,
                    mappedSourceChanged,
                    "equal mapped Source reloads do not raise SourceChanged");
            }
            finally
            {
                runtime.Dispose();
            }

            using (Bitmap applicationImage = new Bitmap(1, 1))
            {
                ImageControl direct = new ImageControl();
                int sourceChanged = 0;
                int stretchChanged = 0;

                try
                {
                    direct.SourceChanged +=
                        delegate(object sender, EventArgs e)
                        {
                            sourceChanged++;
                        };
                    direct.StretchChanged +=
                        delegate(object sender, EventArgs e)
                        {
                            stretchChanged++;
                        };
                    direct.ImageLocation = "obsolete-image.png";
                    direct.Source = applicationImage;

                    AssertTrue(
                        String.IsNullOrEmpty(direct.ImageLocation),
                        "direct Source replaces an obsolete ImageLocation");

                    direct.Source = null;

                    AssertEqual(
                        null,
                        direct.Image,
                        "Source=null clears the image without reloading an obsolete URI");
                    AssertTrue(
                        String.IsNullOrEmpty(direct.ImageLocation),
                        "Source=null leaves no URI to reload on repaint");

                    direct.Source = applicationImage;
                    direct.Stretch = ImageStretch.Fill;

                    AssertSame(
                        applicationImage,
                        direct.Image,
                        "direct Source assignment uses the native Image property");
                    AssertEqual(
                        PictureBoxSizeMode.StretchImage,
                        direct.SizeMode,
                        "direct Stretch assignment uses the native SizeMode property");
                    AssertEqual(
                        3,
                        sourceChanged,
                        "each effective direct Source assignment raises SourceChanged once");
                    AssertEqual(
                        1,
                        stretchChanged,
                        "direct Stretch assignment raises StretchChanged once");

                    direct.SizeMode = PictureBoxSizeMode.CenterImage;

                    AssertEqual(
                        ImageStretch.None,
                        direct.Stretch,
                        "native SizeMode assignments keep Stretch coherent");
                    AssertEqual(
                        2,
                        stretchChanged,
                        "native SizeMode changes raise StretchChanged once");
                }
                finally
                {
                    direct.Dispose();
                }

                AssertEqual(
                    1,
                    applicationImage.Width,
                    "ImageControl does not dispose an application-owned Source");
            }

            TestSharedIconSource();
            TestMutatedByteSourceInvalidatesDecodeCache();
            TestUnchangedLocationReloadIsNoOp();
            TestDisposedTargetsReleaseOwnedImages();
            TestUniformToFillRendering();
            TestThrowingMappedSourceAssignment();
        }

        private static void TestDisposedTargetsReleaseOwnedImages()
        {
            ImageControlOwnershipState state =
                new ImageControlOwnershipState(
                    CreatePngBytes(2, 1, Color.Orange));
            XamlRuntime runtime = null;
            RetainingSourceImageControl first = null;
            RetainingSourceImageControl second = null;
            Image shared = null;

            try
            {
                runtime = XamlRuntime.Load(
                    "<Panel>" +
                    " <RetainingSourceImageControl Name='FirstOwned' " +
                    "   Source='{Binding SourceValue}' " +
                    "   Probe='{Binding ProbeValue, Mode=TwoWay}' />" +
                    " <RetainingSourceImageControl Name='SecondOwned' " +
                    "   Source='{Binding SourceValue}' " +
                    "   Probe='{Binding ProbeValue, Mode=TwoWay}' />" +
                    "</Panel>",
                    state);
                first = runtime.Get<RetainingSourceImageControl>(
                    "FirstOwned");
                second = runtime.Get<RetainingSourceImageControl>(
                    "SecondOwned");
                shared = first.Image;

                AssertSame(
                    shared,
                    second.Image,
                    "the disposal probe starts with one shared generated image");
                AssertSame(
                    shared,
                    GetOwnedImage(runtime, first),
                    "the first live target owns one shared-image reference");
                AssertSame(
                    shared,
                    GetOwnedImage(runtime, second),
                    "the second live target owns one shared-image reference");

                RetainingSourceImageControl.ThrowOnProbeChangedRemove = true;
                first.Dispose();

                AssertEqual(
                    1,
                    first.ProbeChangedRemoveAttemptCount,
                    "external disposal attempts hostile binding-event cleanup");
                AssertEqual(
                    null,
                    GetOwnedImage(runtime, first),
                    "a disposed target releases its generated-image ownership");
                AssertEqual(
                    2,
                    shared.Width,
                    "the shared image remains alive for the second target");
                AssertEqual(
                    1,
                    GetRuntimeCollectionCount(runtime, "_decodedImageCache"),
                    "the live shared image remains in the weak decode cache");

                second.Dispose();

                AssertEqual(
                    1,
                    second.ProbeChangedRemoveAttemptCount,
                    "the last target also attempts hostile binding cleanup");
                AssertEqual(
                    null,
                    GetOwnedImage(runtime, second),
                    "the last disposed target drops its ownership entry");
                AssertImageDisposed(
                    shared,
                    "the last target release disposes the generated image");
                AssertEqual(
                    0,
                    GetRuntimeCollectionCount(runtime, "_decodedImageCache"),
                    "disposing the last owner evicts its weak cache entry");
            }
            finally
            {
                RetainingSourceImageControl.ThrowOnProbeChangedRemove = false;

                if (runtime != null)
                    runtime.Dispose();
            }

            using (Bitmap applicationImage = new Bitmap(3, 1))
            {
                ImageControlOwnershipState applicationState =
                    new ImageControlOwnershipState(applicationImage);
                XamlRuntime applicationRuntime = null;

                try
                {
                    applicationRuntime = XamlRuntime.Load(
                        "<Panel>" +
                        " <Image Name='ApplicationOwned' " +
                        "   Source='{Binding SourceValue}' />" +
                        "</Panel>",
                        applicationState);
                    ImageControl applicationTarget =
                        applicationRuntime.Get<ImageControl>(
                            "ApplicationOwned");

                    AssertEqual(
                        null,
                        GetOwnedImage(applicationRuntime, applicationTarget),
                        "application Image values are never runtime-owned");

                    applicationTarget.Dispose();

                    AssertEqual(
                        3,
                        applicationImage.Width,
                        "target disposal does not dispose an application Image");
                }
                finally
                {
                    if (applicationRuntime != null)
                        applicationRuntime.Dispose();
                }
            }
        }

        private static void TestSharedIconSource()
        {
            ImageControlBindingState state =
                new ImageControlBindingState();
            state.TransactionSource = SystemIcons.Information;

            XamlRuntime runtime = XamlRuntime.Load(
                "<Panel>" +
                " <Image Name='IconImage' " +
                "   Source='{Binding TransactionSource}' />" +
                " <PictureBox Name='IconPictureBox' " +
                "   Source='{Binding TransactionSource}' />" +
                "</Panel>",
                state);
            Image shared = null;

            try
            {
                ImageControl image =
                    runtime.Get<ImageControl>("IconImage");
                PictureBox picture =
                    runtime.Get<PictureBox>("IconPictureBox");
                shared = image.Image;
                int sourceChanged = 0;
                image.SourceChanged +=
                    delegate(object sender, EventArgs e)
                    {
                        sourceChanged++;
                    };

                AssertTrue(
                    shared != null,
                    "an Icon source is converted to a bitmap");
                AssertSame(
                    shared,
                    picture.Image,
                    "Image and PictureBox share one Icon conversion");

                runtime.ReloadBinding("IconImage", "Source");

                AssertSame(
                    shared,
                    image.Image,
                    "reloading the same Icon retains the shared conversion");
                AssertEqual(
                    0,
                    sourceChanged,
                    "an unchanged Icon reload does not raise SourceChanged");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertImageDisposed(
                shared,
                "the shared Icon conversion is released after its last owner");
        }

        private static void TestMutatedByteSourceInvalidatesDecodeCache()
        {
            ImageControlBindingState state =
                new ImageControlBindingState();
            state.SourceBytes = CreateBmpBytes(1, 1, Color.Red);

            XamlRuntime runtime = XamlRuntime.Load(
                "<Image Name='MutableBytes' " +
                " Source='{Binding SourceBytes}' />",
                state);

            try
            {
                ImageControl picture =
                    runtime.Get<ImageControl>("MutableBytes");
                Image initial = picture.Image;
                int sourceChanged = 0;
                picture.SourceChanged +=
                    delegate(object sender, EventArgs e)
                    {
                        sourceChanged++;
                    };

                byte[] replacement = CreateBmpBytes(1, 1, Color.Blue);

                AssertEqual(
                    state.SourceBytes.Length,
                    replacement.Length,
                    "equal-size BMP fixtures permit an in-place byte mutation");

                Array.Copy(
                    replacement,
                    0,
                    state.SourceBytes,
                    0,
                    replacement.Length);
                runtime.ReloadBinding("MutableBytes", "Source");

                AssertTrue(
                    !Object.ReferenceEquals(initial, picture.Image),
                    "an in-place byte mutation invalidates the decoded-image cache");
                AssertEqual(
                    1,
                    sourceChanged,
                    "mutated bytes raise one effective SourceChanged event");
                AssertColorNear(
                    Color.Blue,
                    ((Bitmap)picture.Image).GetPixel(0, 0),
                    "the replacement bitmap reflects the mutated byte content");
                AssertImageDisposed(
                    initial,
                    "replacing mutated bytes retires the previous decoded image");
            }
            finally
            {
                runtime.Dispose();
            }

            AssertEqual(
                null,
                GetRuntimeField(runtime, "_decodedImageCache"),
                "runtime disposal drops remaining weak decode-cache bookkeeping");
        }

        private static void TestUnchangedLocationReloadIsNoOp()
        {
            ImageControlBindingState state =
                new ImageControlBindingState();
            string imagePath = Path.Combine(
                Path.GetTempPath(),
                "wfx-image-source-" +
                Guid.NewGuid().ToString("N") +
                ".bmp");

            using (Bitmap bitmap = new Bitmap(2, 1))
                bitmap.Save(imagePath, ImageFormat.Bmp);

            state.SourcePath = imagePath;
            XamlRuntime runtime = null;
            ImageControl picture = null;

            try
            {
                runtime = XamlRuntime.Load(
                    "<Image Name='LocationImage' WaitOnLoad='true' " +
                    " Source='{Binding SourcePath}' />",
                    state);
                picture = runtime.Get<ImageControl>("LocationImage");
                Image loaded = picture.Image;
                int sourceChanged = 0;
                picture.SourceChanged +=
                    delegate(object sender, EventArgs e)
                    {
                        sourceChanged++;
                    };

                AssertTrue(
                    loaded != null,
                    "WaitOnLoad loads the location before the reload probe");

                runtime.ReloadBinding("LocationImage", "Source");

                AssertSame(
                    loaded,
                    picture.Image,
                    "reloading an unchanged location retains the native image");
                AssertEqual(
                    0,
                    sourceChanged,
                    "an unchanged location reload does not raise SourceChanged");
            }
            finally
            {
                if (picture != null)
                    picture.Dispose();

                if (runtime != null)
                    runtime.Dispose();

                if (File.Exists(imagePath))
                    File.Delete(imagePath);
            }
        }

        private static void TestUniformToFillRendering()
        {
            using (Bitmap source = new Bitmap(40, 20))
            {
                using (Graphics sourceGraphics = Graphics.FromImage(source))
                {
                    sourceGraphics.FillRectangle(Brushes.Red, 0, 0, 10, 20);
                    sourceGraphics.FillRectangle(Brushes.Green, 10, 0, 10, 20);
                    sourceGraphics.FillRectangle(Brushes.Blue, 20, 0, 10, 20);
                    sourceGraphics.FillRectangle(Brushes.Yellow, 30, 0, 10, 20);
                }

                using (ImageControlPaintProbe control =
                    new ImageControlPaintProbe())
                {
                    control.BackColor = Color.Magenta;
                    control.Size = new Size(20, 20);
                    control.Source = source;
                    control.Stretch = ImageStretch.UniformToFill;
                    Image sourceSeenByApplicationPaint = null;
                    int applicationPaintCount = 0;

                    PaintEventHandler applicationPaint =
                        delegate(object sender, PaintEventArgs e)
                        {
                            sourceSeenByApplicationPaint = control.Image;
                            applicationPaintCount++;
                            e.Graphics.FillRectangle(Brushes.White, 0, 0, 1, 1);
                        };
                    control.Paint += applicationPaint;
                    control.BackgroundPaintCount = 0;

                    using (Bitmap square = new Bitmap(20, 20))
                    {
                        control.DrawToBitmap(
                            square,
                            new Rectangle(0, 0, square.Width, square.Height));

                        AssertColorNear(
                            Color.Green,
                            square.GetPixel(4, 10),
                            "UniformToFill crops the wide source's left edge");
                        AssertColorNear(
                            Color.Blue,
                            square.GetPixel(15, 10),
                            "UniformToFill crops the wide source's right edge");
                        AssertColorNear(
                            Color.Green,
                            square.GetPixel(4, 1),
                            "UniformToFill covers the destination top edge");
                        AssertColorNear(
                            Color.White,
                            square.GetPixel(0, 0),
                            "application Paint handlers run after the cover image");
                    }

                    AssertEqual(
                        1,
                        control.BackgroundPaintCount,
                        "UniformToFill paints its background only once");
                    AssertEqual(
                        1,
                        applicationPaintCount,
                        "UniformToFill raises application Paint exactly once");
                    AssertSame(
                        source,
                        sourceSeenByApplicationPaint,
                        "owner painting keeps Source installed for Paint handlers");

                    control.Size = new Size(40, 10);
                    control.BackgroundPaintCount = 0;

                    using (Bitmap wide = new Bitmap(40, 10))
                    {
                        control.DrawToBitmap(
                            wide,
                            new Rectangle(0, 0, wide.Width, wide.Height));

                        AssertColorNear(
                            Color.Red,
                            wide.GetPixel(2, 5),
                            "resizing recalculates the UniformToFill vertical crop");
                        AssertColorNear(
                            Color.Yellow,
                            wide.GetPixel(37, 5),
                            "resized UniformToFill still covers the full width");
                    }

                    AssertEqual(
                        1,
                        control.BackgroundPaintCount,
                        "resized UniformToFill still uses one background pass");
                    AssertEqual(
                        2,
                        applicationPaintCount,
                        "resized UniformToFill keeps one public Paint callback");

                    AssertSame(
                        source,
                        control.Image,
                        "UniformToFill painting and resizing retain the source image");
                }
            }
        }

        private static void TestThrowingMappedSourceAssignment()
        {
            ImageControlBindingState state =
                new ImageControlBindingState();
            state.TransactionSource = CreatePngBytes(1, 1, Color.Red);

            XamlRuntime runtime = XamlRuntime.Load(
                "<Image Name='TransactionalImage' " +
                " Source='{Binding TransactionSource}' />",
                state);
            ImageControl picture = null;
            InvalidateEventHandler invalidated = null;

            try
            {
                picture = runtime.Get<ImageControl>("TransactionalImage");
                picture.CreateControl();
                Image initial = picture.Image;
                bool injected = false;

                invalidated = delegate(object sender, InvalidateEventArgs e)
                {
                    if (!injected &&
                        picture.Image != null &&
                        !Object.ReferenceEquals(picture.Image, initial))
                    {
                        injected = true;
                        throw new InvalidOperationException(
                            "Injected image invalidation failure.");
                    }
                };
                picture.Invalidated += invalidated;
                state.TransactionSource =
                    CreatePngBytes(2, 1, Color.Blue);

                AssertReloadThrows(
                    runtime,
                    "TransactionalImage",
                    "generated image assignment surfaces callback failures");

                Image generated = picture.Image;

                AssertTrue(
                    injected && generated != null && generated.Width == 2,
                    "throw-after-commit keeps the generated image installed");
                AssertSame(
                    generated,
                    GetOwnedImage(runtime, picture),
                    "throw-after-commit tracks the generated image ownership");

                picture.Invalidated -= invalidated;
                invalidated = null;
                state.TransactionSource = null;
                runtime.ReloadBinding(
                    "TransactionalImage",
                    "Source");

                AssertEqual(
                    null,
                    GetOwnedImage(runtime, picture),
                    "clearing Source releases failed-assignment ownership");
                AssertImageDisposed(
                    generated,
                    "clearing Source disposes the tracked generated image");

                EventHandler sourceChangedFailure =
                    delegate(object sender, EventArgs e)
                    {
                        throw new InvalidOperationException(
                            "Injected SourceChanged failure.");
                    };
                picture.SourceChanged += sourceChangedFailure;
                state.TransactionSource =
                    CreatePngBytes(4, 1, Color.Purple);

                AssertReloadThrows(
                    runtime,
                    "TransactionalImage",
                    "SourceChanged callback failures remain observable");

                Image eventFailureImage = picture.Image;

                AssertTrue(
                    eventFailureImage != null &&
                    eventFailureImage.Width == 4,
                    "SourceChanged failures keep the committed image installed");
                AssertSame(
                    eventFailureImage,
                    GetOwnedImage(runtime, picture),
                    "SourceChanged failures preserve generated image ownership");

                picture.SourceChanged -= sourceChangedFailure;
                state.TransactionSource = null;
                runtime.ReloadBinding(
                    "TransactionalImage",
                    "Source");
                AssertImageDisposed(
                    eventFailureImage,
                    "clearing Source releases a SourceChanged-failed image");

                Bitmap applicationImage = new Bitmap(3, 1);

                try
                {
                    injected = false;
                    invalidated =
                        delegate(object sender, InvalidateEventArgs e)
                        {
                            if (!injected &&
                                Object.ReferenceEquals(
                                    picture.Image,
                                    applicationImage))
                            {
                                injected = true;
                                throw new InvalidOperationException(
                                    "Injected application image failure.");
                            }
                        };
                    picture.Invalidated += invalidated;
                    state.TransactionSource = applicationImage;

                    AssertReloadThrows(
                        runtime,
                        "TransactionalImage",
                        "application image assignment surfaces callback failures");

                    AssertTrue(
                        injected && Object.ReferenceEquals(
                            picture.Image,
                            applicationImage),
                        "throw-after-commit keeps the application image installed");
                    AssertEqual(
                        null,
                        GetOwnedImage(runtime, picture),
                        "application images remain outside runtime ownership");

                    picture.Invalidated -= invalidated;
                    invalidated = null;
                    state.TransactionSource = null;
                    runtime.ReloadBinding(
                        "TransactionalImage",
                        "Source");

                    AssertEqual(
                        3,
                        applicationImage.Width,
                        "clearing Source does not dispose an application image");
                }
                finally
                {
                    applicationImage.Dispose();
                }
            }
            finally
            {
                if (picture != null && invalidated != null)
                    picture.Invalidated -= invalidated;

                runtime.Dispose();
            }
        }

        private static byte[] CreatePngBytes(
            int width,
            int height,
            Color color)
        {
            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                bitmap.SetPixel(0, 0, color);
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private static byte[] CreateBmpBytes(
            int width,
            int height,
            Color color)
        {
            using (MemoryStream stream = new MemoryStream())
            using (Bitmap bitmap = new Bitmap(width, height))
            {
                bitmap.SetPixel(0, 0, color);
                bitmap.Save(stream, ImageFormat.Bmp);
                return stream.ToArray();
            }
        }

        private static void AssertReloadThrows(
            XamlRuntime runtime,
            string name,
            string message)
        {
            bool threw = false;

            try
            {
                runtime.ReloadBinding(name, "Source");
            }
            catch (Exception)
            {
                threw = true;
            }

            AssertTrue(threw, message);
        }

        private static Image GetOwnedImage(
            XamlRuntime runtime,
            PictureBox picture)
        {
            FieldInfo ownedValuesField = typeof(XamlRuntime).GetField(
                "_ownedPropertyValues",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Hashtable targets = ownedValuesField == null
                ? null
                : ownedValuesField.GetValue(runtime) as Hashtable;
            Hashtable properties = targets == null
                ? null
                : targets[picture] as Hashtable;
            object entry = properties == null
                ? null
                : properties["Image"];

            if (entry == null)
                return null;

            FieldInfo valueField = entry.GetType().GetField(
                "Value",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);

            return valueField == null
                ? null
                : valueField.GetValue(entry) as Image;
        }

        private static object GetRuntimeField(
            XamlRuntime runtime,
            string name)
        {
            FieldInfo field = typeof(XamlRuntime).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);

            return field == null ? null : field.GetValue(runtime);
        }

        private static int GetRuntimeCollectionCount(
            XamlRuntime runtime,
            string name)
        {
            ICollection collection =
                GetRuntimeField(runtime, name) as ICollection;

            return collection == null ? 0 : collection.Count;
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
                    message + ": expected near '" + expected +
                    "', actual '" + actual + "'.");
            }
        }

        private static void AssertEqual(
            object expected,
            object actual,
            string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    message + ": expected '" + expected +
                    "', actual '" + actual + "'.");
            }
        }

        private static void AssertSame(
            object expected,
            object actual,
            string message)
        {
            if (!Object.ReferenceEquals(expected, actual))
                throw new InvalidOperationException(message + ".");
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message + ".");
        }
    }
}
