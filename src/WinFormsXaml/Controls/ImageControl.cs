using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WinFormsXaml
{
    /// <summary>WPF-style image scaling options.</summary>
    public enum ImageStretch
    {
        /// <summary>Displays the image without scaling it.</summary>
        None,

        /// <summary>Scales the image to the complete control bounds.</summary>
        Fill,

        /// <summary>Scales the image while preserving its aspect ratio.</summary>
        Uniform,

        /// <summary>
        /// Preserves aspect ratio and fills the available area, cropping equal
        /// amounts from opposite edges when the aspect ratios differ.
        /// </summary>
        UniformToFill
    }

    /// <summary>
    /// The PictureBox-derived control created by the WPF-style Image XML
    /// element. Source assignments continue through XamlRuntime's shared
    /// PictureBox image-loading and ownership path.
    /// </summary>
    public class ImageControl : PictureBox
    {
        private bool _applyingStretch;
        private ImageStretch _stretch;
        private Graphics _uniformToFillPaintGraphics;
        private GraphicsState _uniformToFillPaintState;

        /// <summary>
        /// Initializes an Image control with WPF's Uniform stretch default.
        /// </summary>
        public ImageControl()
        {
            _stretch = ImageStretch.Uniform;
            base.SizeMode = PictureBoxSizeMode.Zoom;
            Paint += new PaintEventHandler(PaintUniformToFill);
        }

        /// <summary>
        /// Gets or sets the current image by using WPF's Source terminology.
        /// Application-assigned images remain owned by the application.
        /// </summary>
        [DefaultValue(null)]
        [Category("Appearance")]
        public Image Source
        {
            get { return Image; }
            set
            {
                bool hasImageLocation =
                    !String.IsNullOrEmpty(base.ImageLocation);

                if (Object.ReferenceEquals(Image, value) &&
                    !hasImageLocation)
                {
                    return;
                }

                // Source has one active value in WPF. Clear a previous URI
                // before installing a direct Image so Source=null cannot cause
                // PictureBox to reload an obsolete ImageLocation on repaint.
                if (hasImageLocation)
                {
                    CancelAsync();
                    base.ImageLocation = null;
                }

                if (!Object.ReferenceEquals(Image, value))
                    Image = value;

                OnSourceChanged(EventArgs.Empty);
            }
        }

        /// <summary>Occurs when Source changes.</summary>
        public event EventHandler SourceChanged;

        /// <summary>
        /// Gets or sets how the image is scaled within the control.
        /// </summary>
        [DefaultValue(ImageStretch.Uniform)]
        [Category("Appearance")]
        public ImageStretch Stretch
        {
            get
            {
                PictureBoxSizeMode nativeMode = base.SizeMode;

                if (_stretch == ImageStretch.UniformToFill &&
                    nativeMode == PictureBoxSizeMode.Zoom)
                {
                    return ImageStretch.UniformToFill;
                }

                if (nativeMode == PictureBoxSizeMode.StretchImage)
                    return ImageStretch.Fill;

                if (nativeMode == PictureBoxSizeMode.Zoom)
                    return ImageStretch.Uniform;

                return ImageStretch.None;
            }
            set
            {
                if (value < ImageStretch.None ||
                    value > ImageStretch.UniformToFill)
                {
                    throw new InvalidEnumArgumentException(
                        "value",
                        (int)value,
                        typeof(ImageStretch));
                }

                if (_stretch == value &&
                    base.SizeMode == GetNativeSizeMode(value))
                {
                    return;
                }

                PictureBoxSizeMode previousSizeMode = base.SizeMode;
                _stretch = value;

                _applyingStretch = true;

                try
                {
                    base.SizeMode = GetNativeSizeMode(value);
                }
                finally
                {
                    _applyingStretch = false;
                }

                // Uniform and UniformToFill both use native Zoom so that the
                // PictureBox keeps its URL loading and animation behavior. A
                // transition between them therefore needs an explicit repaint.
                if (previousSizeMode == base.SizeMode)
                    Invalidate();

                OnStretchChanged(EventArgs.Empty);
            }
        }

        /// <summary>Occurs when Stretch changes.</summary>
        public event EventHandler StretchChanged;

        /// <summary>Raises SourceChanged.</summary>
        protected virtual void OnSourceChanged(EventArgs e)
        {
            EventHandler handler = SourceChanged;

            if (handler != null)
                handler(this, e);
        }

        internal void NotifyMappedSourceChanged()
        {
            OnSourceChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Keeps Stretch coherent when application code uses native SizeMode.
        /// </summary>
        protected override void OnSizeModeChanged(EventArgs e)
        {
            base.OnSizeModeChanged(e);

            if (_applyingStretch)
                return;

            ImageStretch stretch = GetStretchFromNativeSizeMode(
                base.SizeMode);

            if (_stretch == stretch)
                return;

            _stretch = stretch;
            OnStretchChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Keeps PictureBox's URI loader and animated-image bookkeeping while
        /// suppressing its Zoom raster for the owner-painted cover path.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_stretch != ImageStretch.UniformToFill ||
                e == null ||
                e.Graphics == null)
            {
                base.OnPaint(e);
                return;
            }

            GraphicsState state = e.Graphics.Save();
            Graphics previousGraphics = _uniformToFillPaintGraphics;
            GraphicsState previousState = _uniformToFillPaintState;
            _uniformToFillPaintGraphics = e.Graphics;
            _uniformToFillPaintState = state;

            try
            {
                // PictureBox.OnPaint still performs lazy URI loading, animation
                // registration, and ImageAnimator.UpdateFrames. Its DrawImage
                // output remains clipped; PaintUniformToFill restores the original
                // state before the public Paint handlers continue.
                e.Graphics.SetClip(
                    Rectangle.Empty,
                    CombineMode.Replace);
                base.OnPaint(e);
            }
            finally
            {
                if (Object.ReferenceEquals(
                        _uniformToFillPaintState,
                        state) &&
                    Object.ReferenceEquals(
                        _uniformToFillPaintGraphics,
                        e.Graphics))
                {
                    _uniformToFillPaintState = previousState;
                    _uniformToFillPaintGraphics = previousGraphics;
                    e.Graphics.Restore(state);
                }
                else
                {
                    _uniformToFillPaintState = previousState;
                    _uniformToFillPaintGraphics = previousGraphics;
                }
            }
        }

        private void PaintUniformToFill(object sender, PaintEventArgs e)
        {
            if (!RestoreUniformToFillPaintState(e))
                return;

            Image image = base.Image;

            if (image == null)
                return;

            // Preserve PictureBox's centered initial/error image behavior while
            // an ImageLocation is loading or after it fails. A loaded image is
            // available before PictureBox raises Paint, so successful loads take
            // the exact UniformToFill path on the same paint pass.
            if (!String.IsNullOrEmpty(base.ImageLocation) &&
                (Object.ReferenceEquals(image, base.InitialImage) ||
                 Object.ReferenceEquals(image, base.ErrorImage)))
            {
                e.Graphics.DrawImage(
                    image,
                    GetCenteredImageRectangle(image));
                return;
            }

            Rectangle destination = GetImageContentRectangle();

            if (destination.Width <= 0 || destination.Height <= 0 ||
                image.Width <= 0 || image.Height <= 0)
            {
                return;
            }

            RectangleF source = GetUniformToFillSourceRectangle(
                image.Width,
                image.Height,
                destination.Width,
                destination.Height);

            // The base Zoom output was clipped. Draw the current
            // PictureBox/ImageAnimator frame once visibly, directly from the
            // original image, without allocating a resized bitmap.
            e.Graphics.DrawImage(
                image,
                destination,
                source.X,
                source.Y,
                source.Width,
                source.Height,
                GraphicsUnit.Pixel);
        }

        private bool RestoreUniformToFillPaintState(PaintEventArgs e)
        {
            GraphicsState state = _uniformToFillPaintState;

            if (state == null ||
                e == null ||
                !Object.ReferenceEquals(
                    _uniformToFillPaintGraphics,
                    e.Graphics))
            {
                return false;
            }

            _uniformToFillPaintState = null;
            _uniformToFillPaintGraphics = null;
            e.Graphics.Restore(state);
            return true;
        }

        private Rectangle GetImageContentRectangle()
        {
            Rectangle content = ClientRectangle;
            content.X += Padding.Left;
            content.Y += Padding.Top;
            content.Width -= Padding.Horizontal;
            content.Height -= Padding.Vertical;
            return content;
        }

        private Rectangle GetCenteredImageRectangle(Image image)
        {
            Rectangle content = GetImageContentRectangle();
            content.X += (content.Width - image.Width) / 2;
            content.Y += (content.Height - image.Height) / 2;
            content.Size = image.Size;
            return content;
        }

        private static RectangleF GetUniformToFillSourceRectangle(
            int imageWidth,
            int imageHeight,
            int destinationWidth,
            int destinationHeight)
        {
            // Use integer arithmetic for the aspect-ratio decision, avoiding
            // float rounding at the equality boundary and normal-size overflow.
            long scaledWidth = (long)imageWidth * (long)destinationHeight;
            long scaledHeight = (long)imageHeight * (long)destinationWidth;

            if (scaledWidth > scaledHeight)
            {
                float sourceWidth =
                    ((float)imageHeight * (float)destinationWidth) /
                    (float)destinationHeight;
                return new RectangleF(
                    ((float)imageWidth - sourceWidth) / 2.0F,
                    0.0F,
                    sourceWidth,
                    (float)imageHeight);
            }

            if (scaledWidth < scaledHeight)
            {
                float sourceHeight =
                    ((float)imageWidth * (float)destinationHeight) /
                    (float)destinationWidth;
                return new RectangleF(
                    0.0F,
                    ((float)imageHeight - sourceHeight) / 2.0F,
                    (float)imageWidth,
                    sourceHeight);
            }

            return new RectangleF(
                0.0F,
                0.0F,
                (float)imageWidth,
                (float)imageHeight);
        }

        /// <summary>Raises StretchChanged.</summary>
        protected virtual void OnStretchChanged(EventArgs e)
        {
            EventHandler handler = StretchChanged;

            if (handler != null)
                handler(this, e);
        }

        private static PictureBoxSizeMode GetNativeSizeMode(
            ImageStretch stretch)
        {
            if (stretch == ImageStretch.None)
                return PictureBoxSizeMode.CenterImage;

            if (stretch == ImageStretch.Fill)
                return PictureBoxSizeMode.StretchImage;

            return PictureBoxSizeMode.Zoom;
        }

        private static ImageStretch GetStretchFromNativeSizeMode(
            PictureBoxSizeMode sizeMode)
        {
            if (sizeMode == PictureBoxSizeMode.StretchImage)
                return ImageStretch.Fill;

            if (sizeMode == PictureBoxSizeMode.Zoom)
                return ImageStretch.Uniform;

            return ImageStretch.None;
        }
    }
}
