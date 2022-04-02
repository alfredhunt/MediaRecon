using ApexBytez.MediaRecon.ViewModel;
using MediaRecon;
using System.Windows.Controls;

namespace ApexBytez.MediaRecon.View
{
    /// <summary>
    /// Interaction logic for SaveView.xaml
    /// </summary>
    public partial class SaveView : UserControl
    {
        public SaveView()
        {
            InitializeComponent();
            this.DataContext = App.Current.Services.GetService(typeof(SaveViewModel));
        }
    }
}
