using ApexBytez.MediaRecon.DB;
using MethodTimer;
using System;
using System.Collections.Concurrent;
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
using static ApexBytez.MediaRecon.Analysis.ConflictedFiles;

namespace ApexBytez.MediaRecon.Analysis
{
    internal class MediaAnalysis : RunnableStep
    {
        private Subject<ConflictedFiles> _conflictResolutions;
        private Subject<ReconciledFile> _reconciledFiles;
        private IDisposable resolutionDisposable;
        private IDisposable reconciledDisposable;
        
        private object tplLock = new object();
        private AnalysisResults currentProgress = new AnalysisResults();
        private long numberOfFilesInStage1;
        private long numberOfFilesInStage2;
        private long stage1FilesProcessed;
        private long stage2FilesProcessed;

        
        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; } = new AnalysisResults();
        private ProcessingStages CurrentStage { get; set; }
        public enum ProcessingStages
        {
            Distinct,
            Duplicates
        }

        public MediaAnalysis(AnalysisOptions analysisOptions)
        {
            AnalysisOptions = analysisOptions;
        }

      

        protected override async Task RunAsyncStep()
        {
            // We also need to evaluate our destination folder... that or 
            //  we need to figure out some other method of handling conflicts
            //  as we move data into it
            var folders = AnalysisOptions.SourceFolders.ToList();
            if (!folders.Contains(AnalysisOptions.DestinationDirectory))
            {
                folders.Add(AnalysisOptions.DestinationDirectory);
            }

            // This sorted list is binned on file name
            var yearMonthSorted = await FileAnalysis.GetYearMonthSortedFileInfo(folders, cancellationToken);

            IEnumerable<KeyValuePair<string, List<FileInfo>>>? flattened = yearMonthSorted.SelectMany(x => x.Value);

            AnalysisResults.NumberOfFilesInAnalysis = flattened.Sum(x => x.Value.Count);
            AnalysisResults.SizeOfDataInAnalysis = flattened.Sum(x => x.Value.Sum(y => y.Length));

            List<FileInfo> distinct = new List<FileInfo>();
            List<List<FileInfo>> duplicates = new();

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

                foreach (var group in yearMonthSorted[key].Values)
                {
                    if (group.Count == 1)
                    {
                        distinct.Add(group.First());
                        numberOfFilesInStage1++;
                    }
                    else
                    {
                        duplicates.Add(group);
                        numberOfFilesInStage2 += group.Count;
                    }
                }
            }

            // Continue to split this up so the progress is for the files being analyzed
            //  and not the ones that are quickly determined to be unique.

            try
            {

                CurrentStage = ProcessingStages.Distinct;
                await ProcessDistinctFilesAsync(distinct);


                CurrentStage = ProcessingStages.Duplicates;
                // What if we still us a semaphore to only allow so many of these
                //  these parallel threads to do the more computationally expensive operation?
                await Parallel.ForEachAsync(duplicates,
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
                    async (fileList, ct) =>
                    {

                        await ProcessNonDistinctFilesAsync2(fileList, ct);
                    });

                var reconciledFileName = AnalysisResults.ReconciledFiles.ToList();
                var fileNameGroups = reconciledFileName.GroupBy(x => x.ReconciledFilePath);
                var fileConflicts = fileNameGroups.Where(x => x.Count() > 1);

                Debug.WriteLine("");
                Debug.WriteLine("Conflicts...");
                foreach (var fileConflict in fileConflicts)
                {
                    // Resolve final conflicts
                    foreach (var file in fileConflict)
                    {
                        Debug.WriteLine("{0}: {1}", file.Name, file.ReconciledFilePath);

                    }
                }
                Debug.WriteLine("Done analysis");
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private Task ProcessNonDistinctFilesAsync2(List<FileInfo> fileList, CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                try
                {
                    Debug.Assert(fileList.Count() > 1);

                    var bitwiseGoupings = await ToBitwiseGroupsAsync2(fileList, ct);

                    if (bitwiseGoupings.Count() == 1)
                    {
                        // Ugly stuff
                        var duplicateFiles = new DuplicateFiles(bitwiseGoupings.First().Select(x => x.FileInfo).ToList());
                        lock (tplLock)
                        {
                            stage2FilesProcessed += duplicateFiles.TotalFileCount;
                            currentProgress.NumberOfFilesAnalyzed += duplicateFiles.TotalFileCount;
                            currentProgress.NumberOfDistinctFiles += duplicateFiles.NumberOfDistinctFiles;
                            currentProgress.NumberOfDuplicateFiles += duplicateFiles.NumberOfDuplicateFiles;
                            currentProgress.SizeOfDistinctFiles += duplicateFiles.Size;
                            currentProgress.SizeOfDuplicateFiles += duplicateFiles.DuplicateFileSystemSize;
                        }

                        await InsertReconciledFileAsync(duplicateFiles);
                    }

                    // If 2+, then we have files with the same name but different data and it gets a bit trickier
                    else if (bitwiseGoupings.Count() > 1)
                    {
                        // We need to figure out renaming here at this level where we can
                        // protect access to a master list of names to avoid conflicts

                        // What if we rename with part of hash  <FileName>-<Hash>.<Extension>
                        // We could be somewhat reassure that we end up with a duplicate that it's
                        // highly likely that it's from a previous run and we should do an extra
                        // comparison on it.

                        var conflictedFiles = new ConflictedFiles(bitwiseGoupings);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AnalysisResults.RenamedFiles.Add(conflictedFiles);
                        });

                        foreach (var file in conflictedFiles.ReconciledFiles)
                        {
                            switch (file.ReconType)
                            {
                                case ReconType.Duplicate:
                                    var duplicateFiles = (DuplicateFiles)file;
                                    lock (tplLock)
                                    {
                                        stage2FilesProcessed += duplicateFiles.TotalFileCount;
                                        currentProgress.NumberOfFilesAnalyzed += duplicateFiles.TotalFileCount;
                                        currentProgress.NumberOfDistinctFiles += duplicateFiles.NumberOfDistinctFiles;
                                        currentProgress.NumberOfDuplicateFiles += duplicateFiles.NumberOfDuplicateFiles;
                                        currentProgress.SizeOfDistinctFiles += duplicateFiles.Size;
                                        currentProgress.SizeOfDuplicateFiles += duplicateFiles.DuplicateFileSystemSize;
                                    }
                                    await InsertReconciledFileAsync(duplicateFiles);
                                    break;
                                case ReconType.Distinct:
                                    var distinctFile = (UniqueFile)file;
                                    lock (tplLock)
                                    {
                                        stage2FilesProcessed++;
                                        currentProgress.NumberOfFilesAnalyzed++;
                                        currentProgress.NumberOfDistinctFiles++;
                                        currentProgress.SizeOfDistinctFiles += distinctFile.Size;
                                    }
                                    await InsertReconciledFileAsync(distinctFile);
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            });

        }

        [Time]
        private async Task ProcessDistinctFilesAsync(List<FileInfo> distinct)
        {
            foreach (var file in distinct)
            {
                var uniqueFile = new UniqueFile(file);
                lock (tplLock)
                {
                    stage1FilesProcessed++;
                    currentProgress.NumberOfFilesAnalyzed++;
                    currentProgress.NumberOfDistinctFiles++;
                    currentProgress.SizeOfDistinctFiles += uniqueFile.Size;
                }
                await InsertReconciledFileAsync(uniqueFile);
            }
        }

        protected override Task UpdateProgress()
        {
            return Task.Run(async () =>
            {
                // TODO: It might look better if the 2 stages were more distinct in the UI
                //  Possible 2 progress bars, single cancel button that spans them
                double progressRatio = 0.0;
                switch (CurrentStage)
                {
                    case ProcessingStages.Distinct:
                        progressRatio = ((double)stage1FilesProcessed / numberOfFilesInStage1);
                        break;
                    case ProcessingStages.Duplicates:
                        progressRatio = ((double)stage2FilesProcessed / numberOfFilesInStage2);
                        break;
                }
                var percentageComplete = progressRatio * 100;
                var progressBarValue = progressRatio * Properties.Settings.Default.ProgressBarMaximum;
                var runTime = DateTime.Now - startTime;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AnalysisResults.NumberOfDistinctFiles = currentProgress.NumberOfDistinctFiles;
                    AnalysisResults.NumberOfDuplicateFiles = currentProgress.NumberOfDuplicateFiles;
                    AnalysisResults.SizeOfDistinctFiles = currentProgress.SizeOfDistinctFiles;
                    AnalysisResults.SizeOfDuplicateFiles = currentProgress.SizeOfDuplicateFiles;
                    AnalysisResults.NumberOfFilesAnalyzed = currentProgress.NumberOfFilesAnalyzed;
                    AnalysisResults.SizeOfDataAnalyzed = AnalysisResults.SizeOfDistinctFiles + AnalysisResults.SizeOfDuplicateFiles;
                    ProgressBarValue = (int)progressBarValue;
                    ProgressPercentage = percentageComplete;
                    RunTime = runTime;
                });
            });
        }

        private async Task InsertReconciledFileAsync(ReconciledFile reconciled)
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
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AnalysisResults.ReconciledDirectories.Add(yearDir);
                        });
                    }
                    else if (isMonthNew)
                    {
                        monthDir.Add(reconciled);
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            yearDir.Add(monthDir);
                        });
                    }
                    else
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            monthDir.Add(reconciled);
                        });
                    }
                    break;
                case SortingStrategy.None:
                default:
                    reconciled.ReconciliationDirectory = AnalysisOptions.DestinationDirectory;
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        AnalysisResults.ReconciledDirectories.Add(reconciled);
                    });
                    break;
            }
        }

       

        public static string ByteArrayToString(byte[] ba)
        {
            return BitConverter.ToString(ba).Replace("-", "");
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
                        try 
                        {
                            // I wonder if I should have just hashed every file, then binned hashes.
                            // That would be time intensive
                            byte[] hash = await GetFileInfoHashAsync(fileInfo);
                            fileHashes.Add(new Tuple<byte[], FileInfo>(hash, fileInfo));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }
                    var hashGroups = fileHashes.GroupBy(x => x.Item1, new ArrayComparer<byte>());
                    foreach (var files in hashGroups)
                    {
                        IEnumerable<FileInfo>? filesPerHash = files.Select(x => x.Item2);
                        bitwiseGoupings.Add(filesPerHash.ToList());
                    }
                }
            }

            return bitwiseGoupings;
        }

        

        private async Task<List<List<ReconFileInfo>>> ToBitwiseGroupsAsync2(List<FileInfo> fileList, CancellationToken cancellationToken)
        {
            var bitwiseGoupings = new List<List<ReconFileInfo>>();

            // filelist here has all of the files with the same name
            // We can group by length
            var groupedByLength = fileList.GroupBy(x => x.Length);

            foreach (var filesWithSameLength in groupedByLength)
            {
                if (filesWithSameLength.Count() == 1)
                {
                    // Only 1 file this length with this name, no hash should match
                    // BUT now with the new naming scheme, we need the hash of this file too
                    byte[] hash = await GetFileInfoHashAsync(filesWithSameLength.First());
                    bitwiseGoupings.Add(new List<ReconFileInfo> { new ReconFileInfo(filesWithSameLength.First(), hash) });
                }
                else
                {
                    List<ReconFileInfo> fileHashes = new List<ReconFileInfo>();
                    foreach (var fileInfo in filesWithSameLength)
                    {
                        try
                        {
                            // I wonder if I should have just hashed every file, then binned hashes.
                            // That would be time intensive
                            byte[] hash = await GetFileInfoHashAsync(fileInfo);
                            fileHashes.Add(new ReconFileInfo(fileInfo, hash));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }
                    var hashGroups = fileHashes.GroupBy(x => x.Hash, new ArrayComparer<byte>());
                    foreach (var files in hashGroups)
                    {
                        List<ReconFileInfo>? filesPerHash = files.ToList();
                        bitwiseGoupings.Add(filesPerHash);
                    }
                }
            }

            return bitwiseGoupings;
        }

        private async Task<byte[]> GetFileInfoHashAsync(FileInfo fileInfo)
        {
            Database database = new Database();
            DB.File? file = await database.GetDBFileInfoAsync(fileInfo);
            if (file == null)
            {
                byte[] hash = null;    
                using (var fs = fileInfo.OpenRead())
                {
                    hash = await SHA256.Create().ComputeHashAsync(fs, cancellationToken);
                }
                file = await database.AddDBFileInfoAsync(fileInfo, hash);
            }
            return file.Hash;
        }

       

    }
}
