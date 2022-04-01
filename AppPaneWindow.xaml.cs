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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Tildetool
{
   public partial class AppPaneWindow : Window
   {
      public AppPaneWindow()
      {
         InitializeComponent();

         //
         DataTemplate? template = Resources["roundbutton"] as DataTemplate;

         ContentControl ctrl = new ContentControl();
         ctrl.ContentTemplate = template;
         Grid.SetColumn(ctrl, 1);
         RootGrid.Children.Add(ctrl);
      }

      private void Window_KeyDown(object sender, KeyEventArgs e)
      {
         switch (e.Key)
         {
            case Key.Escape:
               e.Handled = true;
               Dispatcher.BeginInvoke(Close);
               return;
         }
      }
   }
}
