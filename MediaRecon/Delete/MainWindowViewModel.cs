using ApexBytez.MediaRecon.Events;
using ApexBytez.MediaRecon.View;
using MediaRecon;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Microsoft.WindowsAPICodePack.Dialogs;
using System.Diagnostics;
using System.Collections.Concurrent;
using MethodTimer;
using System.Security.Cryptography;
using System.Threading;
using System.Reactive.Subjects;
using System.Reactive.Linq;

namespace ApexBytez.MediaRecon
{
    internal partial class MainWindowViewModel : ObservableObject,
        IRecipient<MainWindowLoaded>,
        IRecipient<SourceFolderDragDropEvent>,
        IRecipient<SourceFolderDeleteEvent>
    {
        private string _directoryPath = string.Empty;
        private Dictionary<string, List<FileInfo>> _allFileInfo = new Dictionary<string, List<FileInfo>>();

        public ObservableCollection<FileCleanupViewModel> FileCleanup { get; set; } = new();
        public string DirectoryPath { get => _directoryPath; set => SetProperty(ref _directoryPath, value); }

        public ICommand AddSourceFolderCommand { get; set; }
        public ICommand SetSaveFolderCommand { get; set; }
        public ICommand AnalyzeCommand { get; set; }
        public ICommand ReconcileCommand { get; set; }
        public ICommand SaveCommand { get; set; }

        public MainWindowViewModel(MahApps.Metro.Controls.Dialogs.IDialogCoordinator instance)
        {
            // TODO: Enable states for commands
            AddSourceFolderCommand = new RelayCommand(ExecuteAddSourceFolderCommand);
            AnalyzeCommand = new AsyncRelayCommand(ExecuteAnalyzeCommandAsync);
            SaveCommand = new RelayCommand(ExecuteSaveCommand);

            // Register that specific message...
            //WeakReferenceMessenger.Default.Register<MainWindowLoaded>(this);

            // ...or alternatively, register all declared handlers
            WeakReferenceMessenger.Default.RegisterAll(this);

            //SourceFolders.Add(@"F:\Pictures\");
            SourceFolders.Add(@"F:\Pictures\Wedding");
            SourceFolders.Add(@"F:\Pictures\OurWedding");
            //SourceFolders.Add(@"V:\GoPro\2021-12-24\HERO9 Black 1");
            //SourceFolders.Add(@"E:\HERO9 Black 1");

        }

        private void ExecuteAddSourceFolderCommand()
        {
            // Request the value from another module
            //DialogResults<DirectoryInfo> results = WeakReferenceMessenger.Default.Send<SystemFolderUserRequest>();
            //FolderBrowserDialog folderBrowser = new FolderBrowserDialog();
            //var result = folderBrowser.ShowDialog();
            //if (result.HasValue && result.Value && folderBrowser.SelectedFolderPath != null)
            //{
            //    sourceFolders.Add(folderBrowser.SelectedFolderPath);
            //}

            // https://stackoverflow.com/questions/11624298/how-to-use-openfiledialog-to-select-a-folder
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                sourceFolders.Add(dialog.FileName);
                //MessageBox.Show("You selected: " + dialog.FileName);
            }
        }

        // Process as fast as you can .... But don't try to update the UI that fast

        private async Task ExecuteAnalyzeCommandAsync()
        {
            ConflictedFiles.Clear();
            ReconciledFiles.Clear();
            ReconciledDirectories.Clear();

            ProgressBarValue = 0;

            await AnalyzeAsync();
        }


        [Time]
        private async Task AnalyzeAsync()
        {
            // This sorted list is binned on file name
            var sortedFiles = FileAnalysis.GetSortedFileInfo(sourceFolders);
            //var orderedByFileCount = sortedFiles.OrderBy(x => x.Value.Count());
            FileCount = sortedFiles.Sum(x => x.Value.Count);
            SourceFoldersSize = sortedFiles.Sum(x => x.Value.Sum(y => y.Length));
            ProgressBarMaximum = 100;

            TimeSpan elapsedTime = TimeSpan.FromSeconds(0);

            var observableTimer = Observable.Interval(TimeSpan.FromMilliseconds(42))
                .TimeInterval()
                .Subscribe(x =>
                {
                    elapsedTime = elapsedTime.Add(x.Interval);
                    var elapsed = elapsedTime.ToString();
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        AnalysisTime = elapsed;
                    });
                });

            _conflictResolutions = new Subject<ConflictedFiles>();
            resolutionDisposable = _conflictResolutions
                .Subscribe(x =>
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        ConflictedFiles.Add(x);
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
                    StatusBarDescription = string.Format("Processing {0}", fileList.Value.First().FullName);

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
            StatusBarDescription = "Analysis Complete";

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

        private void ExecuteSaveCommand()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
            }
            var destinationFolder = dialog.FileName;
            var filesRemoved = 0;
            var filesSaved = 0;
            // TODO: do something more elegant for the sentinel navigation folders
            foreach (IFolderViewFolder yearDirectory in ReconciledDirectories.Where(x => !x.Name.Equals("..")))
            {
                var yearFolder = Path.Combine(destinationFolder, yearDirectory.Name);

                foreach (IFolderViewFolder monthDirectory in yearDirectory.Items)
                {
                    var monthFolder = Path.Combine(yearFolder, monthDirectory.Name);

                    foreach (ReconciledFile file in monthDirectory.Items.Where(x => x is ReconciledFile))
                    {
                        var destinationFilePath = Path.Combine(monthFolder, file.Name);
                        System.Diagnostics.Debug.WriteLine("Moving: {0} to {1}", file.FullName, destinationFilePath);

                        switch (file.ReconType)
                        {
                            case ReconType.Distinct:
                                // Simple move operation if its not already in the destination location. Remove original location if moved
                                filesSaved++;
                                break;
                            case ReconType.Duplicate:
                                // All of these files are the same, get one into the destination, delete the rest.
                                filesSaved++;
                                var duplicateFiles = file as DuplicateFiles;
                                if (duplicateFiles != null)
                                {
                                    filesRemoved += duplicateFiles.Files.Count() - 1;
                                }
                                break;
                            default:
                                Debug.Assert(true);
                                break;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine("filesSaved: {0}, filesRemoved: {1}", filesSaved, filesRemoved);
            StatusBarDescription = string.Format("filesSaved: {0}, filesRemoved: {1}", filesSaved, filesRemoved);

            // Ideas:
            //  Show status as sentences
            //  Analyzing # folders, containing # files, consuming # bytes on disk
            //      # files analyzed, consuming # bytes on disk in # elapsed time
            //      # distinct files found, consuming # bytes on disk
            //      # duplicate files found, consuming # bytes on disk
        }

        public void Receive(MainWindowLoaded message)
        {
            this.MainWindow = message.MainWindow;
        }

        public void Receive(SourceFolderDragDropEvent message)
        {
            SourceFolders.Add(message.Path);
        }

        public void Receive(SourceFolderDeleteEvent message)
        {
            SourceFolders.RemoveAt(message.Data);
        }

        private void PerformAcceptAllConflictResolutions()
        {
            while (ConflictedFiles.Count() > 0)
            {
                var conflictedFile = ConflictedFiles.First();
                while (conflictedFile.ReconciledFiles.Count > 0)
                {
                    var fileGroup = conflictedFile.ReconciledFiles.First();
                    InsertReconciledFile(fileGroup);
                    conflictedFile.ReconciledFiles.Remove(fileGroup);
                }
                ConflictedFiles.Remove(conflictedFile);
            }
        }

        private void SmartSort(bool isChecked)
        {
            System.Diagnostics.Debug.WriteLine("IsCheck: {0}", isChecked);
            IsSmartSortChecked = isChecked;
        }

        private void PerformRemoveSourceFolder()
        {
            SourceFolders.RemoveAt(SelectedSourceFolderIndex);
            SelectedSourceFolderIndex = -1;
        }

        private ObservableCollection<string> sourceFolders = new ObservableCollection<string>();
        public ObservableCollection<string> SourceFolders { get => sourceFolders; set => SetProperty(ref sourceFolders, value); }

        private ObservableCollection<ConflictedFiles> conflictedFiles = new ObservableCollection<ConflictedFiles>();
        public ObservableCollection<ConflictedFiles> ConflictedFiles { get => conflictedFiles; set => SetProperty(ref conflictedFiles, value); }

        private ObservableCollection<IFolderViewItem> reconciledDirectories = new ObservableCollection<IFolderViewItem>();
        public ObservableCollection<IFolderViewItem> ReconciledDirectories { get => reconciledDirectories; set => SetProperty(ref reconciledDirectories, value); }

        private ObservableCollection<ReconciledFile> reconciledFiles = new ObservableCollection<ReconciledFile>();
        public ObservableCollection<ReconciledFile> ReconciledFiles { get => reconciledFiles; set => SetProperty(ref reconciledFiles, value); }




        private MainWindow MainWindow;

        private RelayCommand removeSourceFolder;
        public ICommand RemoveSourceFolder => removeSourceFolder ??= new RelayCommand(PerformRemoveSourceFolder);



        private int selectedSourceFolderIndex;
        public int SelectedSourceFolderIndex { get => selectedSourceFolderIndex; set => SetProperty(ref selectedSourceFolderIndex, value); }

        private RelayCommand<bool> smartSortCommand;
        public ICommand SmartSortCommand => smartSortCommand ??= new RelayCommand<bool>(SmartSort);



        private bool isSmartSortChecked = true;
        public bool IsSmartSortChecked { get => isSmartSortChecked; set => SetProperty(ref isSmartSortChecked, value); }


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

        private long fileCount1;
        public long FileCount { get => fileCount1; set => SetProperty(ref fileCount1, value); }
        
        
        private IDisposable reconciledDisposable;
        private CancellationToken ct;
        private IDisposable resolutionDisposable;
        Subject<ReconciledFile> _reconciledFiles;
        Subject<ConflictedFiles> _conflictResolutions;

        private object statusBarDescription;

        public object StatusBarDescription { get => statusBarDescription; set => SetProperty(ref statusBarDescription, value); }


    }
}
