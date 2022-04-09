using ApexBytez.MediaRecon.View;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ApexBytez.MediaRecon.Analysis
{
    internal class AnalysisResults : ObservableObject
    {
        private long fileCount;
        private long sourceFoldersSize;
        private long numberOfFiles;
        private long totalSize;
        private long numberOfDuplicateFiles;
        private long duplicateSize;
        private long numberOfDistinctFiles;
        private long distinctSize;
        public long FileCount { get => fileCount; set => SetProperty(ref fileCount, value); }
        public long SourceFoldersSize { get => sourceFoldersSize; set => SetProperty(ref sourceFoldersSize, value); }
        public long NumberOfFiles { get => numberOfFiles; set => SetProperty(ref numberOfFiles, value); }
        public long TotalSize { get => totalSize; set => SetProperty(ref totalSize, value); }
        public long DuplicateCount { get => numberOfDuplicateFiles; set => SetProperty(ref numberOfDuplicateFiles, value); }
        public long DuplicateSize { get => duplicateSize; set => SetProperty(ref duplicateSize, value); }
        public long DistinctCount { get => numberOfDistinctFiles; set => SetProperty(ref numberOfDistinctFiles, value); }
        public long DistinctSize { get => distinctSize; set => SetProperty(ref distinctSize, value); }
        public ObservableCollection<IFolderViewItem> ReconciledDirectories { get; private set; } = new ObservableCollection<IFolderViewItem>();
        public List<ReconciledFile> ReconciledFiles { get; private set; } = new List<ReconciledFile>();
        public ObservableCollection<ConflictedFiles> RenamedFiles { get; private set; } = new ObservableCollection<ConflictedFiles>();

    }
}
