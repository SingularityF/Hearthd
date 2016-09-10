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
    /// Interaction logic for ClickHelper.xaml
    /// </summary>
    public partial class ClickHelper : Window
    {
        int x, y;

        public ClickHelper()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MainWindow.clickPos.x = x;
                MainWindow.clickPos.y = y;
                Close();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var relativePosition = e.GetPosition(this);
            var point = PointToScreen(relativePosition);
            Canvas.SetLeft(Pointer, point.X - Container.PointToScreen(new Point(0, 0)).X - 2.5);
            Canvas.SetTop(Pointer, point.Y - Container.PointToScreen(new Point(0, 0)).Y - 2.5);
            x = (int)point.X;
            y = (int)point.Y;
        }
    }
}
