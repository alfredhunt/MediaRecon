using ApexBytez.MediaRecon.ViewModel;
using MediaRecon;
using System.Windows.Controls;

namespace ApexBytez.MediaRecon.View
{
    /// <summary>
    /// Interaction logic for WelcomeView.xaml
    /// </summary>
    public partial class WelcomeView : UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
            this.DataContext = App.Current.Services.GetService(typeof(WelcomeViewModel));
        }
    }

}
