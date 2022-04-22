using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ApexBytez.MediaRecon
{
    /// <summary>
    /// Adapted from: https://michlg.wordpress.com/2010/01/16/listbox-automatically-scroll-currentitem-into-view/
    /// This class contains a few useful extenders for the ListView
    /// </summary>
    public class ListViewExtenders
    {
        #region Properties

        public static readonly DependencyProperty AutoScrollToCurrentItemProperty = 
            DependencyProperty.RegisterAttached("AutoScrollToCurrentItem", 
                typeof(bool), typeof(ListViewExtenders), 
                new UIPropertyMetadata(default(bool), 
                    OnAutoScrollToCurrentItemChanged));

        /// <summary>
        /// Returns the value of the AutoScrollToCurrentItemProperty
        /// </summary>
        /// <param name="obj">The dependency-object whichs value should be returned</param>
        /// <returns>The value of the given property</returns>
        public static bool GetAutoScrollToCurrentItem(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToCurrentItemProperty);
        }

        /// <summary>
        /// Sets the value of the AutoScrollToCurrentItemProperty
        /// </summary>
        /// <param name="obj">The dependency-object whichs value should be set</param>
        /// <param name="value">The value which should be assigned to the AutoScrollToCurrentItemProperty</param>
        public static void SetAutoScrollToCurrentItem(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToCurrentItemProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// This method will be called when the AutoScrollToCurrentItem
        /// property was changed
        /// </summary>
        /// <param name="s">The sender (the ListView)</param>
        /// <param name="e">Some additional information</param>
        public static void OnAutoScrollToCurrentItemChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            var listBox = s as ListView;
            if (listBox != null)
            {
                var listBoxItems = listBox.Items;
                if (listBoxItems != null)
                {
                    var notifyCollectionChanged = listBoxItems.SourceCollection as INotifyCollectionChanged;
                    if (notifyCollectionChanged != null)
                    {
                        var autoScroll = new NotifyCollectionChangedEventHandler((s1, e2) => OnAutoScroll(listBox));

                        var newValue = (bool)e.NewValue;

                        if (newValue)
                            notifyCollectionChanged.CollectionChanged += autoScroll;
                        else
                            notifyCollectionChanged.CollectionChanged -= autoScroll;
                    }
                }
            }
        }

        private static void OnAutoScroll(ListView listView)
        {
            if (listView.Items.Count > 0)
                listView.ScrollIntoView(listView.Items[listView.Items.Count - 1]);
        }


        #endregion
    }
}
