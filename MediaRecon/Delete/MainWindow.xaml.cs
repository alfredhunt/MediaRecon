using ApexBytez.MediaRecon;
using ApexBytez.MediaRecon.Events;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Toolkit.Mvvm.Messaging;
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

namespace MediaRecon
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            // https://mahapps.com/docs/dialogs/mvvm-dialog
            this.DataContext = new MainWindowViewModel(DialogCoordinator.Instance);

            // https://docs.microsoft.com/en-us/windows/communitytoolkit/mvvm/messenger

            // Register that specific message...
            //WeakReferenceMessenger.Default.Register<SystemFolderUserRequest>(this);

            // ...or alternatively, register all declared handlers
            //WeakReferenceMessenger.Default.RegisterAll(this);

            // Register the receiver in a module
            //WeakReferenceMessenger.Default.Register<MainWindow, SystemFolderUserRequest>(this, (r, m) =>
            //{
            //    // Assume that "CurrentUser" is a private member in our viewmodel.
            //    // As before, we're accessing it through the recipient passed as
            //    // input to the handler, to avoid capturing "this" in the delegate.
            //    //m.Reply(null);
            //});
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new MainWindowLoaded(this));
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
