using ApexBytez.MediaRecon.Events;
using ApexBytez.MediaRecon.ViewModel;
using MediaRecon;
using MethodTimer;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApexBytez.MediaRecon.View
{
    /// <summary>
    /// Interaction logic for SetupView.xaml
    /// </summary>
    public partial class SetupView : UserControl
    {
        [Time]
        public SetupView()
        {
            InitializeComponent();
            this.DataContext = App.Current.Services.GetService(typeof(SetupViewModel));
        }

        private void SourceListBox_DragEnter(object sender, DragEventArgs e)
        {
            ListBox listView = sender as ListBox;
            if (listView != null)
            {
                // If the DataObject contains string data, extract it.
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] draggedItems = (string[])e.Data.GetData(DataFormats.FileDrop);

                    if (draggedItems.All(x => System.IO.Directory.Exists(x)))
                    {
                        e.Effects = DragDropEffects.Link;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
            }
        }

        private void SourceListBox_DragOver(object sender, DragEventArgs e)
        {
            ListBox listView = sender as ListBox;
            if (listView != null)
            {
                // If the DataObject contains string data, extract it.
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] draggedItems = (string[])e.Data.GetData(DataFormats.FileDrop);

                    if (draggedItems.All(x => System.IO.Directory.Exists(x)))
                    {
                        e.Effects = DragDropEffects.Link;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
            }
        }

        private void SourceListBox_Drop(object sender, DragEventArgs e)
        {
            ListBox listView = sender as ListBox;
            if (listView != null)
            {
                // If the DataObject contains string data, extract it.
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] draggedItems = (string[])e.Data.GetData(DataFormats.FileDrop);

                    foreach (var folder in draggedItems)
                    {
                        if (System.IO.Directory.Exists(folder))
                        {
                            WeakReferenceMessenger.Default.Send(new SourceFolderDragDropEvent(folder));
                        }
                    }
                }
            }
        }

        private void SourceListBox_KeyUp(object sender, KeyEventArgs e)
        {
            ListBox listView = sender as ListBox;
            if (listView != null && e.Key.Equals(Key.Delete))
            {
                WeakReferenceMessenger.Default.Send(new SourceFolderDeleteEvent(listView.SelectedIndex));
            }
        }
    }

}
