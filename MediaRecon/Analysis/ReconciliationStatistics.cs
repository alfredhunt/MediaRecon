using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace ApexBytez.MediaRecon.Analysis
{
    internal class ReconciliationStatistics : ObservableObject
    {
        private long filesProcessed;
        private long dataProcessed;
        private long duplicatesDeleted;
        private long duplicateData;
        private long distinctSaved;
        private long distinctData;
        public long FilesProcessed { get => filesProcessed; set => SetProperty(ref filesProcessed, value); }
        public long DataProcessed { get => dataProcessed; set => SetProperty(ref dataProcessed, value); }
        public long DuplicatesDeleted { get => duplicatesDeleted; set => SetProperty(ref duplicatesDeleted, value); }
        public long DuplicateData { get => duplicateData; set => SetProperty(ref duplicateData, value); }
        public long DistinctSaved { get => distinctSaved; set => SetProperty(ref distinctSaved, value); }
        public long DistinctData { get => distinctData; set => SetProperty(ref distinctData, value); }
        public ObservableCollection<string> RemovedItems { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> SavedItems { get; set; } = new ObservableCollection<string>();
    }
}
