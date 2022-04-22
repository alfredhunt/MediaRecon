using ApexBytez.MediaRecon.ViewModel;
using MediaRecon;
using System.Collections.Specialized;
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

            SetAutoScroll(RemovedItemsListView);
            SetAutoScroll(SavedItemsListView);
        }

        private void SetAutoScroll(ListView listView)
        {
            var items = listView.Items;
            if (items != null)
            {
                var notifyCollectionChanged = items.SourceCollection as INotifyCollectionChanged;
                if (notifyCollectionChanged != null)
                {
                    var autoScroll = new NotifyCollectionChangedEventHandler((s1, e2) => OnAutoScroll(listView));

                    notifyCollectionChanged.CollectionChanged += autoScroll;

                }
            }
        }

        private void OnAutoScroll(ListView listView)
        {
            if (listView.Items.Count > 0)
            {
                // TODO: this kills the UI because the updates are not called on the update timer
                listView.ScrollIntoView(listView.Items[listView.Items.Count - 1]);
            }
        }
    }
}
