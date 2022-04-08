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
    public enum DeleteStrategy
    {
        Soft, // Recycle bin
        Hard, // Hard Delete
    }
    public enum MoveStrategy
    {
        Move,
        Copy
    }
    public enum SortingStrategy
    {
        None,
        YearAndMonth
    }
    public enum RunStrategy
    {
        Normal,
        DryRun
    }

    public class AnalysisOptions : ObservableObject
    {
        private ObservableCollection<string> sourceFolders = new ObservableCollection<string>();
        private string destinationFolder = String.Empty;
        private DeleteStrategy deleteStrategy;
        private MoveStrategy moveStrategy;
        private SortingStrategy sortingStrategy = SortingStrategy.YearAndMonth;
        private RunStrategy runStrategy;
        public ObservableCollection<string> SourceFolders { get => sourceFolders; set => SetProperty(ref sourceFolders, value); }
        public string DestinationDirectory { get => destinationFolder; set => SetProperty(ref destinationFolder, value); }
        public DeleteStrategy DeleteStrategy { get => deleteStrategy; set => SetProperty(ref deleteStrategy, value); }
        public MoveStrategy MoveStrategy { get => moveStrategy; set => SetProperty(ref moveStrategy, value); }
        public SortingStrategy SortingStrategy { get => sortingStrategy; set => SetProperty(ref sortingStrategy, value); }
        public RunStrategy RunStrategy { get => runStrategy; set => SetProperty(ref runStrategy, value); }
    }

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
    }

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


    internal abstract class RunnableStep : ObservableObject
    {
        private CancellationTokenSource cancellationTokenSource;
        protected CancellationToken cancellationToken;
        private bool canCancel;
        private bool showResultLabel;
        private string resultsLabel;
        private double progressBarValue;
        private double progressBarMaximum;
        private TimeSpan analysisTime;
        private bool analysisInProgress;
        private RelayCommand cancelCommand;

        public double ProgressBarValue { get => progressBarValue; set => SetProperty(ref progressBarValue, value); }
        public double ProgressBarMaximum { get => progressBarMaximum; set => SetProperty(ref progressBarMaximum, value); }
        public TimeSpan RunTime { get => analysisTime; set => SetProperty(ref analysisTime, value); }
        public bool Running { get => analysisInProgress; set => SetProperty(ref analysisInProgress, value); }
        public bool CanCancel { get => canCancel; set => SetProperty(ref canCancel, value); }
        public bool ShowResultsLabel { get => showResultLabel; set => SetProperty(ref showResultLabel, value); }
        public string ResultsLabel { get => resultsLabel; set => SetProperty(ref resultsLabel, value); }
        public RelayCommand CancelCommand => cancelCommand ??= new RelayCommand(Cancel, CanExecuteCancel);
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
        protected abstract Task RunAsyncStep();
        public async Task RunAsync()
        {
            await Run(RunAsyncStep);
        }
        protected async Task Run(Func<Task> asyncTask)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            ProgressBarValue = 0;
            ProgressBarMaximum = 100;
            RunTime = TimeSpan.Zero;
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
                        RunTime = elapsedTime;
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
    }

    internal class SaveResults : RunnableStep
    {
        private Subject<ReconciledFile> subject;
        private IDisposable subjectDisposable;

        public AnalysisResults AnalysisResults { get; private set; }
        public AnalysisOptions AnalysisOptions { get; private set; }
        public ReconciliationStatistics ReconStats { get; set; } = new ReconciliationStatistics();
        public ObservableCollection<string> RemovedItems { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> SavedItems { get; set; } = new ObservableCollection<string>();

        public SaveResults(AnalysisOptions analysisOptions, AnalysisResults analysisResults)
        {
            AnalysisOptions = analysisOptions;
            AnalysisResults = analysisResults;
        }

        protected override async Task RunAsyncStep()
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
                switch (AnalysisOptions.SortingStrategy)
                {
                    case SortingStrategy.YearAndMonth:
                        foreach (var year in AnalysisResults.ReconciledDirectories
                        .Where(x => x is IFolderViewFolder)
                        .Select(x => x as IFolderViewFolder))
                        {
                            var yearDir = Path.Combine(AnalysisOptions.DestinationDirectory, year.Name);
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
                        break;
                    case SortingStrategy.None:
                    default:
                        break;
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

                        var progressBarValue = ((double)ReconStats.FilesProcessed / AnalysisResults.FileCount) * 100;
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

                await Parallel.ForEachAsync(AnalysisResults.ReconciledFiles,
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
    }

    internal class Analysis : RunnableStep
    {
        private Subject<ConflictedFiles> _conflictResolutions;
        private Subject<ReconciledFile> _reconciledFiles;
        private IDisposable resolutionDisposable;
        private IDisposable reconciledDisposable;

        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; } = new AnalysisResults();

        public Analysis(AnalysisOptions analysisOptions)
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

                    var progressBarValue = ((double)numberOfFiles / AnalysisResults.FileCount) * 100;
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
