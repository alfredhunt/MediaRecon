using ApexBytez.MediaRecon.View;
using MvvmWizard.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ApexBytez.MediaRecon.ViewModel
{
    internal class AnalysisViewModel : StepViewModelBase
    {
        public override async Task OnTransitedFrom(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep < transitionContext.TransitedFromStep)
            {
                // Moving back
                return;
            }

            if (transitionContext.IsSkipAction)
            {
                // Skip button clicked
                return;
            }

            transitionContext.SharedContext["RenamedFiles"] = conflictedFiles.AsEnumerable();
            transitionContext.SharedContext["Reconciled"] = ReconciledDirectories.AsEnumerable();

            // Save data here
            await Task.Delay(0);
        }

        public override Task OnTransitedTo(TransitionContext transitionContext)
        {
            if (transitionContext.TransitToStep < transitionContext.TransitedFromStep)
            {
                // Moving backwards
                return base.OnTransitedTo(transitionContext);
            }

            var folders = transitionContext.SharedContext["Folders"] as IEnumerable<string>;

            Debug.Assert(folders != null);

            // Start the analysis...
            Task.Run(() => AnalyzeAsync(folders));

            // Load data here
            return base.OnTransitedTo(transitionContext);
        }

        private async Task AnalyzeAsync(IEnumerable<string> folders)
        {
            ProgressBarValue = 0;
            ProgressBarMaximum = 100;
            FolderCount = folders.Count();

            // This sorted list is binned on file name
            var sortedFiles = FileAnalysis.GetSortedFileInfo(folders);
            //var orderedByFileCount = sortedFiles.OrderBy(x => x.Value.Count());
            FileCount = sortedFiles.Sum(x => x.Value.Count);
            SourceFoldersSize = sortedFiles.Sum(x => x.Value.Sum(y => y.Length));            

            TimeSpan elapsedTime = TimeSpan.FromSeconds(0);

            var observableTimer = Observable.Interval(TimeSpan.FromMilliseconds(42))
                .TimeInterval()
                .Subscribe(x =>
                {
                    elapsedTime = elapsedTime.Add(x.Interval);
                    var elapsed = elapsedTime.ToString();
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // TODO: Make this human readable
                        AnalysisTime = elapsed;
                    });
                });

            _conflictResolutions = new Subject<ConflictedFiles>();
            resolutionDisposable = _conflictResolutions
                .Subscribe(x =>
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    { 
                        // TODO: Refactor candidate. If not showing this in new wizard layout can probably get rid of this observable
                        conflictedFiles.Add(x);
                    });
                });

            // Santiy Check
            long total = 0;

            _reconciledFiles = new Subject<ReconciledFile>();
            reconciledDisposable = _reconciledFiles
                .Buffer(TimeSpan.FromMilliseconds(24))
                //.Buffer(1)
                .Where(x => x.Count > 0)
                .Subscribe(x =>
                {
                    long distinctCount = DistinctCount;
                    long distinctSize = DistinctSize;
                    long duplicateCount = DuplicateCount;
                    long duplicateSize = DuplicateSize;
                    long numberOfFiles = NumberOfFiles;

                    foreach (var file in x)
                    {
                        switch (file.ReconType)
                        {
                            case ReconType.Duplicate:
                                var duplicateFiles = (DuplicateFiles)file;
                                InsertReconciledFile(duplicateFiles);
                                numberOfFiles += duplicateFiles.TotalFileCount;
                                distinctCount += duplicateFiles.NumberOfDistinctFiles;
                                duplicateCount += duplicateFiles.NumberOfDuplicateFiles;
                                distinctSize += duplicateFiles.Size;
                                duplicateSize += duplicateFiles.DuplicateFileSystemSize;

                                total += duplicateFiles.Files.Count();
                                break;
                            case ReconType.Distinct:
                                var distinctFile = (UniqueFile)file;
                                InsertReconciledFile(distinctFile);
                                numberOfFiles++;
                                distinctCount++;
                                distinctSize += distinctFile.Size;

                                total++;
                                break;
                        }
                    }

                    var progressBarValue = ((double)numberOfFiles / FileCount) * 100;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DistinctCount = distinctCount;
                        DuplicateCount = duplicateCount;
                        DistinctSize = distinctSize;
                        DuplicateSize = duplicateSize;
                        NumberOfFiles = numberOfFiles;
                        TotalSize = DistinctSize + DuplicateSize;
                        ProgressBarValue = progressBarValue;
                    });
                });

            // TODO: Implement cancel from UI
            ct = new CancellationToken();
            await Parallel.ForEachAsync(sortedFiles,
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                async (fileList, ct) =>
                {
                    //StatusBarDescription = string.Format("Processing {0}", fileList.Value.First().FullName);

                    if (fileList.Value.Count() == 1)
                    {
                        var uniqueFile = new UniqueFile(fileList.Value.First());
                        _reconciledFiles.OnNext(uniqueFile);
                    }
                    // If 2+ items, we analyze
                    else if (fileList.Value.Count() > 1)
                    {
                        var bitwiseGoupings = await ToBitwiseGroupsAsync(fileList.Value, ct);
                        if (bitwiseGoupings.Count() == 1)
                        {
                            var duplicateFiles = new DuplicateFiles(bitwiseGoupings.First());
                            _reconciledFiles.OnNext(duplicateFiles);
                        }

                        // If 2+, then we have files with the same name but different data and it gets a bit trickier
                        else if (bitwiseGoupings.Count() > 1)
                        {
                            var conflictedFiles = new ConflictedFiles(bitwiseGoupings);
                            _conflictResolutions.OnNext(conflictedFiles);
                            foreach (var file in conflictedFiles.ReconciledFiles)
                            {
                                _reconciledFiles.OnNext(file);
                            }
                        }
                    }
                });

            observableTimer.Dispose();
            //StatusBarDescription = "Analysis Complete";

        }

        private void InsertReconciledFile(ReconciledFile reconciled)
        {
            bool isYearNew = false;
            bool isMonthNew = false;
            var year = reconciled.LastWriteTime.Year.ToString();
            var month = reconciled.LastWriteTime.ToString("MMMM");

            var yearDir = ReconciledDirectories.FirstOrDefault(x => x.Name.Equals(year)) as ReconciledDirectory;
            if (yearDir == null)
            {
                var sentinel = new ReconciledDirectory("..", ReconciledDirectories);
                yearDir = new ReconciledDirectory(year, sentinel);
                isYearNew = true;
            }

            var monthDir = yearDir.Items.FirstOrDefault(x => x.Name.Equals(month)) as ReconciledDirectory;
            if (monthDir == null)
            {
                var sentinel = new ReconciledDirectory("..", yearDir.Items);
                monthDir = new ReconciledDirectory(month, sentinel);
                isMonthNew = true;
            }

            // If year is new, you can make whole structure before dispatch invoking
            // If the month is new, you only need to dispatch on the year, not month
            if (isYearNew)
            {
                monthDir.Add(reconciled);
                yearDir.Add(monthDir);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReconciledDirectories.Add(yearDir);
                });
            }
            else if (isMonthNew)
            {
                monthDir.Add(reconciled);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    yearDir.Add(monthDir);
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    monthDir.Add(reconciled);
                });
            }
        }

        private async Task<List<List<FileInfo>>> ToBitwiseGroupsAsync(List<FileInfo> fileList, CancellationToken cancellationToken)
        {
            var bitwiseGoupings = new List<List<FileInfo>>();

            // filelist here has all of the files with the same name
            // We can group by length
            var groupedByLength = fileList.GroupBy(x => x.Length);

            foreach (var filesWithSameLength in groupedByLength)
            {
                if (filesWithSameLength.Count() == 1)
                {
                    // Only 1 file this length with this name, no hash should match
                    bitwiseGoupings.Add(new List<FileInfo> { filesWithSameLength.First() });
                }
                else
                {
                    List<Tuple<byte[], FileInfo>> fileHashes = new List<Tuple<byte[], FileInfo>>();
                    foreach (var fileInfo in filesWithSameLength)
                    {
                        byte[] hash;
                        using (var fs = fileInfo.OpenRead())
                        {
                            hash = await MD5.Create().ComputeHashAsync(fs, cancellationToken);
                        }
                        fileHashes.Add(new Tuple<byte[], FileInfo>(hash, fileInfo));
                    }
                    foreach (var files in fileHashes.GroupBy(x => x.Item1, new ArrayComparer<byte>()))
                    {
                        IEnumerable<FileInfo>? filesPerHash = files.Select(x => x.Item2);
                        bitwiseGoupings.Add(filesPerHash.ToList());
                    }
                }
            }

            return bitwiseGoupings;
        }

        class ArrayComparer<T> : IEqualityComparer<T[]>
        {
            public bool Equals(T[] x, T[] y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(T[] obj)
            {
                return obj.Aggregate(string.Empty, (s, i) => s + i.GetHashCode(), s => s.GetHashCode());
            }
        }



        private double progressBarValue;

        public double ProgressBarValue { get => progressBarValue; set => SetProperty(ref progressBarValue, value); }

        private double progressBarMaximum;

        public double ProgressBarMaximum { get => progressBarMaximum; set => SetProperty(ref progressBarMaximum, value); }

        private long numberOfFolders;

        public long NumberOfFolders { get => numberOfFolders; set => SetProperty(ref numberOfFolders, value); }

        private long numberOfFiles;

        public long NumberOfFiles { get => numberOfFiles; set => SetProperty(ref numberOfFiles, value); }

        private long totalSize;

        public long TotalSize { get => totalSize; set => SetProperty(ref totalSize, value); }

        private long numberOfDuplicateFiles;

        public long DuplicateCount { get => numberOfDuplicateFiles; set => SetProperty(ref numberOfDuplicateFiles, value); }

        private long duplicateSize;

        public long DuplicateSize { get => duplicateSize; set => SetProperty(ref duplicateSize, value); }

        private long numberOfDistinctFiles;

        public long DistinctCount { get => numberOfDistinctFiles; set => SetProperty(ref numberOfDistinctFiles, value); }

        private long distinctSize;

        public long DistinctSize { get => distinctSize; set => SetProperty(ref distinctSize, value); }

        private string analysisTime;

        public string AnalysisTime { get => analysisTime; set => SetProperty(ref analysisTime, value); }

        private long sourceFoldersSize;

        public long SourceFoldersSize { get => sourceFoldersSize; set => SetProperty(ref sourceFoldersSize, value); }

        private long fileCount;
        public long FileCount { get => fileCount; set => SetProperty(ref fileCount, value); }


        private ObservableCollection<IFolderViewItem> reconciledDirectories = new ObservableCollection<IFolderViewItem>();
        private IEnumerable<string>? folders;
        private Subject<ConflictedFiles> _conflictResolutions;
        private IDisposable resolutionDisposable;
        private Subject<ReconciledFile> _reconciledFiles;
        private IDisposable reconciledDisposable;
        private CancellationToken ct;

        public ObservableCollection<IFolderViewItem> ReconciledDirectories { get => reconciledDirectories; set => SetProperty(ref reconciledDirectories, value); }

        private List<ConflictedFiles> conflictedFiles = new List<ConflictedFiles>();

        private long folderCount;

        public long FolderCount { get => folderCount; set => SetProperty(ref folderCount, value); }
    }
}
