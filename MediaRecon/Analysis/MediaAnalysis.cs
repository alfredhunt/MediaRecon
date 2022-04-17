﻿using MethodTimer;
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

namespace ApexBytez.MediaRecon.Analysis
{
    internal class MediaAnalysis : RunnableStep
    {
        private Subject<ConflictedFiles> _conflictResolutions;
        private Subject<ReconciledFile> _reconciledFiles;
        private IDisposable resolutionDisposable;
        private IDisposable reconciledDisposable;
        private long numberOfFilesToAnalysis;

        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; } = new AnalysisResults();

        public MediaAnalysis(AnalysisOptions analysisOptions)
        {
            AnalysisOptions = analysisOptions;
        }

        protected override async Task RunAsyncStep()
        {
            // This sorted list is binned on file name
            var yearMonthSorted = FileAnalysis.GetYearMonthSortedFileInfo(AnalysisOptions.SourceFolders);

            IEnumerable<KeyValuePair<string, List<FileInfo>>>? flattened = yearMonthSorted.SelectMany(x => x.Value);

            AnalysisResults.FileCount = flattened.Sum(x => x.Value.Count);
            AnalysisResults.SourceFoldersSize = flattened.Sum(x => x.Value.Sum(y => y.Length));

            List<FileInfo> distinct = new List<FileInfo>();
            List<List<FileInfo>> duplicates = new ();

            // Maybe use DateTime objects as keys instead of a string?
            //  otherwise we need to convert our string key here to a date time
            foreach (var key in yearMonthSorted.Keys)
            {
                var dateTime = DateTime.Parse(key);

                Debug.WriteLine(dateTime.ToString());

                var year = dateTime.Year.ToString();
                var month = dateTime.ToString("MMMM");

                var yearDir = AnalysisResults.ReconciledDirectories.FirstOrDefault(x => x.Name.Equals(year)) as ReconciledDirectory;
                if (yearDir == null)
                {
                    var sentinel = new ReconciledDirectory("..", AnalysisResults.ReconciledDirectories);
                    yearDir = new ReconciledDirectory(year, sentinel);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AnalysisResults.ReconciledDirectories.Add(yearDir);
                    });
                }

                var monthDir = yearDir.Items.FirstOrDefault(x => x.Name.Equals(month)) as ReconciledDirectory;
                if (monthDir == null)
                {
                    var sentinel = new ReconciledDirectory("..", yearDir.Items);
                    monthDir = new ReconciledDirectory(month, sentinel);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        yearDir.Add(monthDir);
                    });
                }

                foreach(var group in yearMonthSorted[key].Values)
                {
                    if (group.Count == 1)
                    {
                        distinct.Add(group.First());
                    }
                    else
                    {
                        duplicates.Add(group);
                        numberOfFilesToAnalysis += group.Count;
                    }
                }
            }

            _conflictResolutions = new Subject<ConflictedFiles>();
            resolutionDisposable = _conflictResolutions
                .Subscribe(x =>
                {
                    // Probably don't need this observable
                    AnalysisResults.RenamedFiles.Add(x);
                    
                });

            _reconciledFiles = new Subject<ReconciledFile>();
            reconciledDisposable = _reconciledFiles
                .Buffer(TimeSpan.FromMilliseconds(24),50)
                .Where(x => x.Count > 0)
                .Subscribe(x =>
                {
                    ProcessReconciledFiles(x);
                });

            // Continue to split this up so the progress is for the files being analyzed
            //  and not the ones that are quickly determined to be unique.
            
            try
            {
                foreach (var file in distinct)
                {
                    var uniqueFile = new UniqueFile(file);
                    _reconciledFiles.OnNext(uniqueFile);
                }

                // What if we still us a semaphore to only allow so many of these
                //  these parallel threads to do the more computationally expensive operation?
                await Parallel.ForEachAsync(duplicates,
                    new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
                    async (fileList, ct) =>
                    {
                        Debug.Assert(fileList.Count() > 1);
                        
                        var bitwiseGoupings = await ToBitwiseGroupsAsync(fileList, ct);
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
                        
                    });
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
            }

            

        }

        protected async Task RunAsyncStepOriginal()
        {
            
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
                    new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken },
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
        [Time]
        private void ProcessReconciledFiles(IList<ReconciledFile> x)
        {
            // TOOD: Lets split this up into stages to make the progress more accurate
            // Total Runtime + Step progress and maybe runtime.
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

            //var progressRatio = ((double)numberOfFiles / AnalysisResults.FileCount);
            //var percentageComplete = progressRatio * 100;
            //var progressBarValue = progressRatio * Properties.Settings.Default.ProgressBarMaximum;

            var progressRatio = ((double)AnalysisResults.DuplicateCount / numberOfFilesToAnalysis);
            var percentageComplete = progressRatio * 100;
            var progressBarValue = progressRatio * Properties.Settings.Default.ProgressBarMaximum;

            Application.Current.Dispatcher.Invoke(() =>
            {
                AnalysisResults.DistinctCount = distinctCount;
                AnalysisResults.DuplicateCount = duplicateCount;
                AnalysisResults.DistinctSize = distinctSize;
                AnalysisResults.DuplicateSize = duplicateSize;
                AnalysisResults.NumberOfFiles = numberOfFiles;
                AnalysisResults.TotalSize = AnalysisResults.DistinctSize + AnalysisResults.DuplicateSize;
                ProgressBarValue = (int)progressBarValue;
                ProgressPercentage = percentageComplete;
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
                        Application.Current.Dispatcher.BeginInvoke(() =>
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
                    var hashGroups = fileHashes.GroupBy(x => x.Item1, new ArrayComparer<byte>());
                    Debug.WriteLine("{0}: {1} files in {2} hash groups", 
                        filesWithSameLength.First().Name,
                        fileHashes.Count,
                        hashGroups.Count());
                    foreach (var files in hashGroups)
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
