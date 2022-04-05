using ApexBytez.MediaRecon.View;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
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
    internal class Analysis : ObservableObject
    {
        private long folderCount;
        private long fileCount;
        private long sourceFoldersSize;
        private long numberOfFiles;
        private long totalSize;
        private long numberOfDuplicateFiles;
        private long duplicateSize;
        private long numberOfDistinctFiles;
        private long distinctSize;
        private double progressBarValue;
        private double progressBarMaximum;
        private TimeSpan analysisTime;

        private Subject<ConflictedFiles> _conflictResolutions;
        private Subject<ReconciledFile> _reconciledFiles;

        private IDisposable resolutionDisposable;
        private IDisposable reconciledDisposable;

        private bool sortByDate = true;

        private string destinationFolder;
        private bool analysisInProgress;

        public string DestinationDirectory { get => destinationFolder; set => SetProperty(ref destinationFolder, value); }

        private ObservableCollection<string> sourceFolders = new ObservableCollection<string>();
        public ObservableCollection<string> SourceFolders { get => sourceFolders; set => SetProperty(ref sourceFolders, value); }

        private ObservableCollection<IFolderViewItem> reconciledDirectories = new ObservableCollection<IFolderViewItem>();
        public ObservableCollection<IFolderViewItem> ReconciledDirectories { get => reconciledDirectories; set => SetProperty(ref reconciledDirectories, value); }

        private ObservableCollection<ConflictedFiles> renamedFiles = new ObservableCollection<ConflictedFiles>();

        public ObservableCollection<ConflictedFiles> RenamedFiles { get => renamedFiles; set => SetProperty(ref renamedFiles, value); }

        private ObservableCollection<string> removedItems = new ObservableCollection<string>();
        public ObservableCollection<string> RemovedItems { get => removedItems; set => SetProperty(ref removedItems, value); }

        private ObservableCollection<string> savedItems = new ObservableCollection<string>();
        public ObservableCollection<string> SavedItems { get => savedItems; set => SetProperty(ref savedItems, value); }

        public long FolderCount { get => folderCount; set => SetProperty(ref folderCount, value); }
        public long FileCount { get => fileCount; set => SetProperty(ref fileCount, value); }
        public long SourceFoldersSize { get => sourceFoldersSize; set => SetProperty(ref sourceFoldersSize, value); }
        public long NumberOfFiles { get => numberOfFiles; set => SetProperty(ref numberOfFiles, value); }
        public long TotalSize { get => totalSize; set => SetProperty(ref totalSize, value); }
        public long DuplicateCount { get => numberOfDuplicateFiles; set => SetProperty(ref numberOfDuplicateFiles, value); }
        public long DuplicateSize { get => duplicateSize; set => SetProperty(ref duplicateSize, value); }
        public long DistinctCount { get => numberOfDistinctFiles; set => SetProperty(ref numberOfDistinctFiles, value); }
        public long DistinctSize { get => distinctSize; set => SetProperty(ref distinctSize, value); }
        public double ProgressBarValue { get => progressBarValue; set => SetProperty(ref progressBarValue, value); }
        public double ProgressBarMaximum { get => progressBarMaximum; set => SetProperty(ref progressBarMaximum, value); }
        public TimeSpan AnalysisTime { get => analysisTime; set => SetProperty(ref analysisTime, value); }
        public bool SortByDate { get => sortByDate; set => SetProperty(ref sortByDate, value); }
        public bool Running { get => analysisInProgress; set => SetProperty(ref analysisInProgress, value); }

        private RelayCommand cancelCommand;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        public RelayCommand CancelCommand => cancelCommand ??= new RelayCommand(Cancel, CanExecuteCancel);

        private Subject<ReconciledFile> subject;
        private IDisposable subjectDisposable;

        public List<ReconciledFile> ReconciledFiles { get; private set; } = new List<ReconciledFile>();

        public void Cancel()
        {
            cancellationTokenSource.Cancel();
        }

        public bool CanExecuteCancel()
        {
            return CanCancel = Running &&
                cancellationTokenSource != null &&
                !cancellationTokenSource.IsCancellationRequested;
        }

        private async Task Run(Func<Task> asyncTask)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            ProgressBarValue = 0;
            ProgressBarMaximum = 100;
            Running = true;

            Application.Current.Dispatcher.Invoke(() =>
            {
                CancelCommand.NotifyCanExecuteChanged();
            });

            TimeSpan elapsedTime = TimeSpan.FromSeconds(0);
            var observableTimer = Observable.Interval(TimeSpan.FromMilliseconds(42))
                .TimeInterval()
                .Subscribe(x =>
                {
                    elapsedTime = elapsedTime.Add(x.Interval);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        AnalysisTime = elapsedTime;
                    });
                });

            try
            {
                await asyncTask();
                ResultsLabel = "Done!";
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
                ResultsLabel = "Cancelled";
            }
            finally
            {
                observableTimer.Dispose();
            }

            // TODO: probably need to work on the statefulness of this processing and the UI elements
            //  visibility.
            Running = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                CancelCommand.NotifyCanExecuteChanged();
            });
            Debug.Assert(!CanCancel);
            CanCancel = false;
            ShowResultsLabel = true;
        }

        public async Task SaveResultsAsync()
        {
            await Run(ProcessResultsAsync);
        }
        public async Task RunAnalysisAsync()
        {
            await Run(PerformAnalysisAsync);
        }

        private async Task ProcessResultsAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RemovedItems.Clear();
            });
            string formatFolderExist = "Directory Exists: {0}";
            string formatFolderCreated = "Created Directory: {0}";
            string logMessage = string.Empty;
            try
            {
                // Would possible be best if we could created all the directories first then really use
                //  the power of parallel processing on the files in a flat list
                if (SortByDate)
                {
                    foreach (var year in ReconciledDirectories
                        .Where(x => x is IFolderViewFolder)
                        .Select(x => x as IFolderViewFolder))
                    {
                        var yearDir = Path.Combine(DestinationDirectory, year.Name);
                        var yearDirExist = Directory.Exists(yearDir);

                        if (!yearDirExist)
                        {
                            Directory.CreateDirectory(yearDir);
                        }

                        logMessage = string.Format(yearDirExist ? formatFolderExist : formatFolderCreated, yearDir);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SavedItems.Add(logMessage);
                        });



                        foreach (var month in year.Items
                            .Where(x => x is IFolderViewFolder && !x.Name.Equals(".."))
                            .Select(x => x as IFolderViewFolder))
                        {
                            var monthDir = Path.Combine(yearDir, month.Name);
                            var monthDirExist = Directory.Exists(monthDir);

                            if (!monthDirExist)
                            {
                                Directory.CreateDirectory(monthDir);
                            }

                            logMessage = string.Format(monthDirExist ? formatFolderExist : formatFolderCreated, monthDir);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SavedItems.Add(logMessage);
                            });
                        }
                    }
                }

                // Can we use FileInfo here? for things that are saved we can get updated, delete we use old
                // and we can also pass some status to show saved/delete...  Might work
                subject = new Subject<ReconciledFile>();
                subjectDisposable = subject.Buffer(TimeSpan.FromMilliseconds(24))
                    .Where(x => x.Count > 0)
                    .Subscribe(x =>
                    {

                        long filesProcessed = ReconStats.FilesProcessed;
                        long dataProcessed = ReconStats.DataProcessed;
                        long duplicatesDeleted = ReconStats.DuplicatesDeleted;
                        long duplicateData = ReconStats.DuplicateData;
                        long distinctSaved = ReconStats.DistinctSaved;
                        long distinctData = ReconStats.DistinctData;

                        foreach (var file in x)
                        {
                            switch (file.ReconType)
                            {
                                case ReconType.Distinct:
                                    filesProcessed++;
                                    dataProcessed += file.Size;

                                    break;
                                case ReconType.Duplicate:
                                    var duplicate = file as DuplicateFiles;
                                    filesProcessed += duplicate.TotalFileCount;
                                    dataProcessed += duplicate.TotalFileSystemSize;
                                    duplicatesDeleted += duplicate.NumberOfDuplicateFiles;
                                    duplicateData += duplicate.DuplicateFileSystemSize;
                                    distinctSaved += duplicate.NumberOfDistinctFiles;
                                    distinctData += duplicate.Size;
                                    break;
                                default:
                                    Debug.Assert(false);
                                    break;
                            }
                        }

                        var progressBarValue = ((double)ReconStats.FilesProcessed / FileCount) * 100;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ReconStats.FilesProcessed = filesProcessed;
                            ReconStats.DataProcessed = dataProcessed;
                            ReconStats.DuplicatesDeleted = duplicatesDeleted;
                            ReconStats.DuplicateData = duplicateData;
                            ReconStats.DistinctSaved = distinctSaved;
                            ReconStats.DistinctData = distinctData;
                            ProgressBarValue = progressBarValue;
                        });
                    });

                await Parallel.ForEachAsync(ReconciledFiles,
                      new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = cancellationToken },
                      async (file, ct) =>
                      {
                        // I want to do the work of copy/moving/deleting here...
                        //  BUT... we cant update UI all willy nilly here. We need to maybe use another observable subject
                        //  to streamline the events and update stats just like in the main analysis block
                        string message = string.Empty;

                          try
                          {
                              File.Copy(file.FullName, file.ReconciledFilePath, false);
                              message = string.Format("Moved {0} from {1} to {2}",
                                  file.Name,
                                  file.FullName,
                                  file.ReconciledFilePath);
                              Application.Current.Dispatcher.Invoke(() =>
                              {
                                  SavedItems.Add(message);
                              });
                          }
                          catch (Exception ex)
                          {
                              Debug.WriteLine(ex.ToString());
                          }
                          

                          switch (file.ReconType)
                          {
                              case ReconType.Distinct:

                                  break;
                              case ReconType.Duplicate:
                                  var duplicate = file as DuplicateFiles;
                                  try
                                  {
                                      foreach (var item in duplicate.Files.Skip(1))
                                      {
                                          FileOperationAPIWrapper.MoveToRecycleBin(item.FullName);
                                      }
                                      message = string.Format("Removing {0} duplicate(s) of {1}",
                                          duplicate.NumberOfDuplicateFiles,
                                          duplicate.Name);
                                      Application.Current.Dispatcher.Invoke(() =>
                                      {
                                          RemovedItems.Add(message);
                                      });
                                  }
                                  catch (Exception ex)
                                  {
                                      Debug.WriteLine(ex.ToString());
                                  }
                                  break;
                          }

                          subject.OnNext(file);
                      });
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async Task PerformAnalysisAsync()
        {
            ResetAnlysis();

            FolderCount = SourceFolders.Count();

            // This sorted list is binned on file name
            var sortedFiles = FileAnalysis.GetSortedFileInfo(SourceFolders);
            FileCount = sortedFiles.Sum(x => x.Value.Count);
            SourceFoldersSize = sortedFiles.Sum(x => x.Value.Sum(y => y.Length));

            _conflictResolutions = new Subject<ConflictedFiles>();
            resolutionDisposable = _conflictResolutions
                .Subscribe(x =>
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // TODO: Refactor candidate. If not showing this in new wizard layout can probably get rid of this observable
                        // Would have to use a concurrent object if not observable
                        RenamedFiles.Add(x);
                    });
                });

            _reconciledFiles = new Subject<ReconciledFile>();
            reconciledDisposable = _reconciledFiles
                .Buffer(TimeSpan.FromMilliseconds(24))
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
        private void ResetAnlysis()
        {
            FolderCount = 0;
            FileCount = 0;
            SourceFoldersSize = 0;
            NumberOfFiles = 0;
            TotalSize = 0;
            DuplicateCount = 0;
            DuplicateSize = 0;
            DistinctCount = 0;
            DistinctSize = 0;
            ProgressBarValue = 0;
            ProgressBarMaximum = 100;
            AnalysisTime = TimeSpan.Zero;
        }

        private void InsertReconciledFile(ReconciledFile reconciled)
        {
            ReconciledFiles.Add(reconciled);

            if (!sortByDate)
            {
                reconciled.ReconciliationDirectory = DestinationDirectory;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReconciledDirectories.Add(reconciled);
                });
            }
            else
            {
                bool isYearNew = false;
                bool isMonthNew = false;
                var year = reconciled.LastWriteTime.Year.ToString();
                var month = reconciled.LastWriteTime.ToString("MMMM");

                reconciled.ReconciliationDirectory = Path.Combine(DestinationDirectory, year, month);

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

        public ReconciliationStatistics ReconStats { get; set; } = new ReconciliationStatistics();

        private bool canCancel;

        public bool CanCancel { get => canCancel; set => SetProperty(ref canCancel, value); }

        private bool showResultLabel;

        public bool ShowResultsLabel { get => showResultLabel; set => SetProperty(ref showResultLabel, value); }

        private string resultsLabel;

        public string ResultsLabel { get => resultsLabel; set => SetProperty(ref resultsLabel, value); }

    }

    class ReconciliationStatistics : ObservableObject
    {
        private long filesProcessed;

        public long FilesProcessed { get => filesProcessed; set => SetProperty(ref filesProcessed, value); }

        private long dataProcessed;

        public long DataProcessed { get => dataProcessed; set => SetProperty(ref dataProcessed, value); }

        private long duplicatesDeleted;

        public long DuplicatesDeleted { get => duplicatesDeleted; set => SetProperty(ref duplicatesDeleted, value); }

        private long duplicateData;

        public long DuplicateData { get => duplicateData; set => SetProperty(ref duplicateData, value); }

        private long distinctSaved;

        public long DistinctSaved { get => distinctSaved; set => SetProperty(ref distinctSaved, value); }

        private long distinctData;

        public long DistinctData { get => distinctData; set => SetProperty(ref distinctData, value); }
    }

    class AnalysisStatistics
    { }

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
}
