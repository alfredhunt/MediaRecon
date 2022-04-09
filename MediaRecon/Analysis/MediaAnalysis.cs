using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ApexBytez.MediaRecon.Analysis
{
    internal class MediaAnalysis : RunnableStep
    {
        private Subject<ConflictedFiles> _conflictResolutions;
        private Subject<ReconciledFile> _reconciledFiles;
        private IDisposable resolutionDisposable;
        private IDisposable reconciledDisposable;

        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; } = new AnalysisResults();

        public MediaAnalysis(AnalysisOptions analysisOptions)
        {
            AnalysisOptions = analysisOptions;
        }

        protected override async Task RunAsyncStep()
        {
            // This sorted list is binned on file name
            var sortedFiles = FileAnalysis.GetSortedFileInfo(AnalysisOptions.SourceFolders);
            AnalysisResults.FileCount = sortedFiles.Sum(x => x.Value.Count);
            AnalysisResults.SourceFoldersSize = sortedFiles.Sum(x => x.Value.Sum(y => y.Length));

            _conflictResolutions = new Subject<ConflictedFiles>();
            resolutionDisposable = _conflictResolutions
                .Subscribe(x =>
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // TODO: Refactor candidate. If not showing this in new wizard layout can probably get rid of this observable
                        // Would have to use a concurrent object if not observable
                        AnalysisResults.RenamedFiles.Add(x);
                    });
                });

            _reconciledFiles = new Subject<ReconciledFile>();
            reconciledDisposable = _reconciledFiles
                .Buffer(TimeSpan.FromMilliseconds(24))
                .Where(x => x.Count > 0)
                .Subscribe(x =>
                {
                    ProcessReconciledFiles(x);
                });

            try
            {
                await Parallel.ForEachAsync(sortedFiles,
                    new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken },
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
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
  
        private void ProcessReconciledFiles(IList<ReconciledFile> x)
        {
            long distinctCount = AnalysisResults.DistinctCount;
            long distinctSize = AnalysisResults.DistinctSize;
            long duplicateCount = AnalysisResults.DuplicateCount;
            long duplicateSize = AnalysisResults.DuplicateSize;
            long numberOfFiles = AnalysisResults.NumberOfFiles;

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

                        break;
                    case ReconType.Distinct:
                        var distinctFile = (UniqueFile)file;
                        InsertReconciledFile(distinctFile);
                        numberOfFiles++;
                        distinctCount++;
                        distinctSize += distinctFile.Size;
                        break;
                }
            }

            var progressBarValue = ((double)numberOfFiles / AnalysisResults.FileCount) * Properties.Settings.Default.ProgressBarMaximum;
            Application.Current.Dispatcher.Invoke(() =>
            {
                AnalysisResults.DistinctCount = distinctCount;
                AnalysisResults.DuplicateCount = duplicateCount;
                AnalysisResults.DistinctSize = distinctSize;
                AnalysisResults.DuplicateSize = duplicateSize;
                AnalysisResults.NumberOfFiles = numberOfFiles;
                AnalysisResults.TotalSize = AnalysisResults.DistinctSize + AnalysisResults.DuplicateSize;
                ProgressBarValue = progressBarValue;
            });
        }

        private void InsertReconciledFile(ReconciledFile reconciled)
        {
            AnalysisResults.ReconciledFiles.Add(reconciled);

            switch (AnalysisOptions.SortingStrategy)
            {
                case SortingStrategy.YearAndMonth:
                    bool isYearNew = false;
                    bool isMonthNew = false;
                    var year = reconciled.LastWriteTime.Year.ToString();
                    var month = reconciled.LastWriteTime.ToString("MMMM");

                    reconciled.ReconciliationDirectory = Path.Combine(AnalysisOptions.DestinationDirectory, year, month);

                    var yearDir = AnalysisResults.ReconciledDirectories.FirstOrDefault(x => x.Name.Equals(year)) as ReconciledDirectory;
                    if (yearDir == null)
                    {
                        var sentinel = new ReconciledDirectory("..", AnalysisResults.ReconciledDirectories);
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
                            AnalysisResults.ReconciledDirectories.Add(yearDir);
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
                    break;
                case SortingStrategy.None:
                default:
                    reconciled.ReconciliationDirectory = AnalysisOptions.DestinationDirectory;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AnalysisResults.ReconciledDirectories.Add(reconciled);
                    });
                    break;
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

    }
}
