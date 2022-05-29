using ApexBytez.MediaRecon.View;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ApexBytez.MediaRecon.Analysis
{
    internal class AnalysisResults : ObservableObject
    {
        private long numberOfFilesInAnalysis;
        private long sourceFoldersSize;
        private long numberOfFilesAnalyzed;
        private long sizeOfDataAnalyzed;
        private long numberOfDuplicateFiles;
        private long sizeOfDuplicateFiles;
        private long numberOfDistinctFiles;
        private long sizeOfDistinctFiles;
        /// <summary>
        /// The number of files in this analysis
        /// </summary>
        public long NumberOfFilesInAnalysis { get => numberOfFilesInAnalysis; set => SetProperty(ref numberOfFilesInAnalysis, value); }
        /// <summary>
        /// The number of files that have been analyzed
        /// </summary>
        public long NumberOfFilesAnalyzed { get => numberOfFilesAnalyzed; set => SetProperty(ref numberOfFilesAnalyzed, value); }
        /// <summary>
        /// The number of duplicate files found during the analysis
        /// </summary>
        public long NumberOfDuplicateFiles { get => numberOfDuplicateFiles; set => SetProperty(ref numberOfDuplicateFiles, value); }
        /// <summary>
        /// The number of distinct files found during the analysis
        /// </summary>
        public long NumberOfDistinctFiles { get => numberOfDistinctFiles; set => SetProperty(ref numberOfDistinctFiles, value); }
        /// <summary>
        /// The total size of all data in this analysis
        /// </summary>
        public long SizeOfDataInAnalysis { get => sourceFoldersSize; set => SetProperty(ref sourceFoldersSize, value); }
        /// <summary>
        /// The size of the data that has been analyzed
        /// </summary>
        public long SizeOfDataAnalyzed { get => sizeOfDataAnalyzed; set => SetProperty(ref sizeOfDataAnalyzed, value); }
        /// <summary>
        /// Size of all duplicate files found in this analysis
        /// </summary>
        public long SizeOfDuplicateFiles { get => sizeOfDuplicateFiles; set => SetProperty(ref sizeOfDuplicateFiles, value); }
        /// <summary>
        /// Size of all distinct files found in this analysis
        /// </summary>
        public long SizeOfDistinctFiles { get => sizeOfDistinctFiles; set => SetProperty(ref sizeOfDistinctFiles, value); }
        /// <summary>
        /// This is really just for the second part of the processing where we have
        /// potential duplicates and conflicts
        /// </summary>

        public ObservableCollection<IFolderViewItem> ReconciledDirectories { get; set; } = new ObservableCollection<IFolderViewItem>();
        public ConcurrentBag<ReconciledFile> ReconciledFiles { get; private set; } = new ConcurrentBag<ReconciledFile>();
        public ObservableCollection<ConflictedFiles> RenamedFiles { get; private set; } = new ObservableCollection<ConflictedFiles>();
        
    }
}
