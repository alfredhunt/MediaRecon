using ApexBytez.MediaRecon.ViewModel;
using MediaRecon;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace ApexBytez.MediaRecon.View
{
    /// <summary>
    /// Interaction logic for ReviewAndSaveView.xaml
    /// </summary>
    public partial class ReviewView : UserControl
    {
        public ReviewView()
        {
            InitializeComponent();
            this.DataContext = App.Current.Services.GetService(typeof(ReviewViewModel));
        }
       
    }
}
