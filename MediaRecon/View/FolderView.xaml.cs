using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApexBytez.MediaRecon.View
{
    /// <summary>
    /// Interaction logic for FolderView.xaml
    /// </summary>
    public partial class FolderView : UserControl
    {
        public FolderView()
        {
            InitializeComponent();

            DataGrid.ItemsSource = ItemsSource;

        }

        public enum FolderViewStyle
        {
            Detailed,
            List,
            Icons
        }

        public FolderViewStyle ViewStyle
        {
            get => (FolderViewStyle)GetValue(ViewStyleProperty);
            set => SetValue(ViewStyleProperty, value);
        }

        public static readonly DependencyProperty ViewStyleProperty = DependencyProperty.Register(
            nameof(ViewStyle), 
            typeof(FolderViewStyle),
            typeof(FolderView), 
            new PropertyMetadata(FolderViewStyle.Detailed, OnViewStyleChanged)
            );

        // Property-changed callback.
        private static void OnViewStyleChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(nameof(OnViewStyleChanged));
        }

        public ObservableCollection<IFolderViewItem> ItemsSource
        {
            get => (ObservableCollection<IFolderViewItem>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), 
            typeof(ObservableCollection<IFolderViewItem>),
            typeof(FolderView),
            new PropertyMetadata(null, OnItemsSourceChanged)
            );

        // Property-changed callback.
        private static void OnItemsSourceChanged(DependencyObject depObj, DependencyPropertyChangedEventArgs e)
        {
            var folderView = depObj as FolderView;
            if (folderView != null)
            {
                folderView.DataGrid.ItemsSource = (ObservableCollection<IFolderViewItem>)e.NewValue;
            }
        }

        public IFolderViewItem SelectedItem
        {
            get { return (IFolderViewItem)GetValue(MyPropertyProperty); }
            set { SetValue(MyPropertyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MyPropertyProperty =
            DependencyProperty.Register(
                nameof(SelectedItem), 
                typeof(IFolderViewItem), 
                typeof(FolderView));

        private void DataGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                DataGrid dg = sender as DataGrid;
                IFolderViewItem folderItem = dg.SelectedItem as IFolderViewItem;
                switch (folderItem.Type)
                {
                    case FolderViewItemType.Folder:
                        // Show these items
                        DataGrid.ItemsSource = ((IFolderViewFolder)folderItem).Items;

                        //foreach (var item in ((IFolderViewFolder)folderItem).Items)
                        //{
                        //    ListView.Items.Add(item);
                        //}


                        break;
                    case FolderViewItemType.File:
                        try
                        {
                            //System.Diagnostics.Process.Start(folderItem.Path);
                            System.Diagnostics.Process photoViewer = new System.Diagnostics.Process();
                            photoViewer.StartInfo.FileName = @"explorer.exe";
                            photoViewer.StartInfo.Arguments = folderItem.FullName;
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
                        break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        break;
                }

            }
            }


        //private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        //{
        //    if (sender is ListViewItem)
        //    {
        //        IFolderViewItem? folderItem = ((ListViewItem)sender).DataContext as IFolderViewItem;
        //        if (folderItem != null)
        //        {
        //            switch (folderItem.Type)
        //            {
        //                case FolderViewItemType.Folder:
        //                    // Show these items
        //                    ListView.ItemsSource = ((IFolderViewFolder)folderItem).Items;

        //                    //foreach (var item in ((IFolderViewFolder)folderItem).Items)
        //                    //{
        //                    //    ListView.Items.Add(item);
        //                    //}


        //                    break;
        //                case FolderViewItemType.File:
        //                    try
        //                    {
        //                        //System.Diagnostics.Process.Start(folderItem.Path);
        //                        System.Diagnostics.Process photoViewer = new System.Diagnostics.Process();
        //                        photoViewer.StartInfo.FileName = @"explorer.exe";
        //                        photoViewer.StartInfo.Arguments = folderItem.FullName;
        //                        photoViewer.Start();
        //                    }
        //                    catch (System.ComponentModel.Win32Exception noBrowser)
        //                    {
        //                        if (noBrowser.ErrorCode == -2147467259)
        //                            MessageBox.Show(noBrowser.Message);
        //                    }
        //                    catch (System.Exception other)
        //                    {
        //                        MessageBox.Show(other.Message);
        //                    }
        //                    break;
        //                default:
        //                    System.Diagnostics.Debug.Assert(false);
        //                    break;
        //            }
        //        }
        //    }
        //}

    }
    public enum FolderViewItemType
    {
        Folder,
        File,
        ParentFolder //?  Would really like to navigate up somehow in that view. ".." would be easy just unsure best way to insert.
    }

    public interface IFolderViewItem
    {
        string Name { get; }
        string FullName { get; }
        DateTime LastWriteTime { get; }
        FolderViewItemType Type { get; }
        long Size { get; }
    }

    public interface IFolderViewFolder : IFolderViewItem
    {
        ObservableCollection<IFolderViewItem> Items { get; }
    }
}
