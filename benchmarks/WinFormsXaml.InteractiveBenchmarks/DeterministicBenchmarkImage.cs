using System.Drawing;

namespace WinFormsXaml.InteractiveBenchmarks
{
    internal static class DeterministicBenchmarkImage
    {
        public static Image Create()
        {
            Bitmap image = new Bitmap(32, 32);

            using (Graphics graphics = Graphics.FromImage(image))
            using (Brush background = new SolidBrush(Color.FromArgb(37, 99, 235)))
            using (Brush foreground = new SolidBrush(Color.White))
            using (Pen border = new Pen(Color.FromArgb(30, 64, 175)))
            {
                graphics.FillRectangle(background, 0, 0, 32, 32);
                graphics.FillEllipse(foreground, 7, 7, 18, 18);
                graphics.DrawRectangle(border, 0, 0, 31, 31);
            }

            return image;
        }
    }
}
