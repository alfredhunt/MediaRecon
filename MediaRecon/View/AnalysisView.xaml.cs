using ApexBytez.MediaRecon.ViewModel;
using MediaRecon;
using System.Windows.Controls;

namespace ApexBytez.MediaRecon.View
{
    /// <summary>
    /// Interaction logic for AnalysisView.xaml
    /// </summary>
    public partial class AnalysisView : UserControl
    {
        public AnalysisView()
        {
            InitializeComponent();
            this.DataContext = App.Current.Services.GetService(typeof(AnalysisViewModel));
        }
    }
}
