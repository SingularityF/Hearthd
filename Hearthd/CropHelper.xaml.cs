using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Hearthd
{
    /// <summary>
    /// Interaction logic for CropHelper.xaml
    /// </summary>
    public partial class CropHelper : Window
    {
        int x1, y1, x2, y2;
        bool beginCrop = false;

        public CropHelper()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MainWindow.selection.left = Math.Min(x1, x2);
                MainWindow.selection.right = Math.Max(x1, x2);
                MainWindow.selection.bottom = Math.Max(y1, y2);
                MainWindow.selection.top = Math.Min(y1, y2);
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var relativePosition = e.GetPosition(this);
            var point = PointToScreen(relativePosition);
            x1 = (int)point.X;
            y1 = (int)point.Y;
            beginCrop = true;
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            beginCrop = false;
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (beginCrop)
            {
                var relativePosition = e.GetPosition(this);
                var point = PointToScreen(relativePosition);
                x2 = (int)point.X;
                y2 = (int)point.Y;
                if (x2 > x1)
                {
                    Canvas.SetLeft(Marquee, x1 - Container.PointToScreen(new Point(0, 0)).X);
                }
                else
                {
                    Canvas.SetLeft(Marquee, x2 - Container.PointToScreen(new Point(0, 0)).X);
                }
                if (y1 < y2)
                {
                    Canvas.SetTop(Marquee, y1 - Container.PointToScreen(new Point(0, 0)).Y);
                }
                else
                {
                    Canvas.SetTop(Marquee, y2 - Container.PointToScreen(new Point(0, 0)).Y);
                }
                Marquee.Height = Math.Abs(y2 - y1);
                Marquee.Width = Math.Abs(x2 - x1);
            }
        }
    }
}
