using MediaRecon.ViewModel;
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

namespace MediaRecon.View
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
