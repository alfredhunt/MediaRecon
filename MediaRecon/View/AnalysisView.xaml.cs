using ApexBytez.MediaRecon.ViewModel;
using MediaRecon;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        private void DuplicateFiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem)
            {
                FileInfo? fileInfo = ((ListViewItem)sender).DataContext as FileInfo;
                if (fileInfo != null)
                {
                    try
                    {
                        //System.Diagnostics.Process.Start(folderItem.Path);
                        System.Diagnostics.Process photoViewer = new System.Diagnostics.Process();
                        photoViewer.StartInfo.FileName = @"explorer.exe";
                        photoViewer.StartInfo.Arguments = fileInfo.FullName;
                        photoViewer.Start();
                    }
                    catch (System.ComponentModel.Win32Exception noBrowser)
                    {
                        if (noBrowser.ErrorCode == -2147467259)
                            MessageBox.Show(noBrowser.Message);
                    }
                    catch (System.Exception other)
                    {
                        MessageBox.Show(other.Message);
                    }
                }
            }
        }

    }
}
