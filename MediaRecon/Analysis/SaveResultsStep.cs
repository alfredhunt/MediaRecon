using ApexBytez.MediaRecon.View;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using System.Windows;
using ApexBytez.MediaRecon.IO;
using System.Collections.Generic;
using MethodTimer;
using ApexBytez.MediaRecon.Extensions;
using System.Threading;
using System.Collections.Concurrent;
using ApexBytez.MediaRecon.DB;

namespace ApexBytez.MediaRecon.Analysis
{
    internal class SaveResultsStep : RunnableStep
    {
        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; }
        public ReconciliationStatistics ReconStats { get; set; } = new ReconciliationStatistics();

        private ReconciliationStatistics currentProgress = new ReconciliationStatistics();

        private object tplLock = new object();

        private ConcurrentQueue<string> moveCopyMessageQueue = new ();
        private ConcurrentQueue<string> recycleDeleteMessageQueue = new();

        private Subject<string> moveCopyMessageSubject = new();
        private Subject<string> recycleDeleteMessageSubject = new();

        public SaveResultsStep(AnalysisOptions analysisOptions, AnalysisResults analysisResults)
        {
            AnalysisOptions = analysisOptions;
            AnalysisResults = analysisResults;
        }

        protected override async Task RunAsyncStep()
        {
            string formatFolderExist = "Directory Exists: {0}";
            string formatFolderCreated = "Created Directory: {0}";
            string logMessage = string.Empty;
            try
            {
                var moveCopyMessageDisposable = moveCopyMessageSubject
                    .Buffer(TimeSpan.FromMilliseconds(24))
                    .Where(x => x.Any())
                    .Subscribe(async x =>
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var z in x)
                                ReconStats.SavedItems.Add(z);
                        });

                    });

                var recycleDeleteMessageDisposable = recycleDeleteMessageSubject
                    .Buffer(TimeSpan.FromMilliseconds(24))
                    .Where(x => x.Any())
                    .Subscribe(async x =>
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var z in x)
                                ReconStats.RemovedItems.Add(z);
                        });
                    });


                // Would possible be best if we could created all the directories first then really use
                //  the power of parallel processing on the files in a flat list
                // TODO: We should be able to have a list of directories from the initial parsing and 
                // not have to go through all of this here.
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
                                //ReconStats.SavedItems.Add(logMessage);
                                moveCopyMessageSubject.OnNext(logMessage);
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
                                    moveCopyMessageSubject.OnNext(logMessage);
                                    //ReconStats.SavedItems.Add(logMessage);
                                });
                            }
                        }
                        break;
                    case SortingStrategy.None:
                    default:
                        break;
                }

                await Parallel.ForEachAsync(AnalysisResults.ReconciledFiles,
                      new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = cancellationToken },
                      async (file, ct) =>
                      {
                          try
                          {
                              // TODO: Consider removing copy or at least warn since it makes the problem worse
                              // Move/Copy One
                              await MoveOrCopyAsync(file.Files.First(), file.ReconciledFilePath, cancellationToken);

                              // Recycle/Delete the rest
                              foreach (var item in file.Files.Skip(1))
                              {
                                  // TODO: do we want or need to produce a list of all files remove?
                                  // Might be a good idea
                                  await RecycleOrDeleteAsync(item, cancellationToken);
                              }
                          }
                          catch (Exception ex)
                          {
                              Debug.WriteLine(ex);
                          }
                      });

                await Task.Delay(100);
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
        private async Task RecycleOrDeleteAsync(FileInfo fileInfo, CancellationToken cancellationToken)
        {
            var message = string.Empty;
            switch (AnalysisOptions.RunStrategy)
            {
                case RunStrategy.Normal:
                    switch (AnalysisOptions.DeleteStrategy)
                    {
                        case DeleteStrategy.Recycle:
                            if (!FileOperationAPIWrapper.MoveToRecycleBin(fileInfo.FullName))
                            {
                                Debug.WriteLine("Failed to recycle {0}", fileInfo.FullName); ;
                            }
                            break;
                        case DeleteStrategy.Delete:
                            fileInfo.Delete();
                            break;
                    }

                    message = string.Format("{0} {1}",
                                AnalysisOptions.DeleteStrategy == DeleteStrategy.Recycle ? "Recycled" : "Delete",
                                fileInfo.FullName);
                    break;
                case RunStrategy.DryRun:
                    // Simulate some cost of copying or moving
                    await Task.Delay(Properties.Settings.Default.DryRunDelay, cancellationToken);
                    message = string.Format("[Simulation] {0} {1}",
                            AnalysisOptions.DeleteStrategy == DeleteStrategy.Recycle ? "Recycled" : "Delete",
                            fileInfo.FullName);
                    break;
                default:
                    // Shouldn't ever get here.
                    break;
            }

            recycleDeleteMessageSubject.OnNext(message);

            lock (tplLock)
            {
                currentProgress.FilesProcessed++;
                currentProgress.DataProcessed += fileInfo.Length;
                currentProgress.DuplicatesDeleted++;
                currentProgress.DuplicateData += fileInfo.Length;
            }
        }

        private async Task MoveOrCopyAsync(FileInfo fileInfo, string destination, CancellationToken cancellationToken)
        {
            var message = string.Empty;
            switch (AnalysisOptions.RunStrategy)
            {
                case RunStrategy.Normal:

                    if (fileInfo.FullName.Equals(destination))
                    {
                        message = string.Format("{0} is already in the destination",
                            fileInfo.FullName);
                    }
                    else 
                    {
                        switch (AnalysisOptions.MoveStrategy)
                        {
                            case MoveStrategy.Copy:
                                fileInfo.Copy(destination, false);
                                break;
                            case MoveStrategy.Move:
                                fileInfo.Move(destination, false);
                                break;
                        }

                        Database database = new Database();
                        database.UpdateDBFileInfo(fileInfo, new FileInfo(destination));

                        message = string.Format("{0} {1} to {2}",
                            AnalysisOptions.MoveStrategy == MoveStrategy.Move ? "Moved" : "Copied",
                            fileInfo.FullName,
                            destination);
                        
                    }
                    break;
                case RunStrategy.DryRun:
                    // Simulate some cost of copying or moving
                    await Task.Delay(Properties.Settings.Default.DryRunDelay, cancellationToken);
                    message = string.Format("[Simulation] {0} {1} to {2}",
                            AnalysisOptions.MoveStrategy == MoveStrategy.Move ? "Moved" : "Copied",
                            fileInfo.FullName,
                            destination);
                    break;
                default:
                    // Shouldn't ever get here.
                    break;
            }

            moveCopyMessageSubject.OnNext(message);

            lock (tplLock)
            {
                currentProgress.FilesProcessed++;
                currentProgress.DataProcessed += fileInfo.Length;
                currentProgress.DistinctSaved++;
                currentProgress.DistinctData += fileInfo.Length;
            }
        }

        protected override Task UpdateProgress()
        {
            return Task.Run(async () =>
            {
                var progressRatio = ((double)currentProgress.FilesProcessed / AnalysisResults.FileCount);
                var percentageComplete = progressRatio * 100;
                var progressBarValue = progressRatio * Properties.Settings.Default.ProgressBarMaximum;
                var runTime = DateTime.Now - startTime;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ReconStats.FilesProcessed = currentProgress.FilesProcessed;
                    ReconStats.DataProcessed = currentProgress.DataProcessed;
                    ReconStats.DuplicatesDeleted = currentProgress.DuplicatesDeleted;
                    ReconStats.DuplicateData = currentProgress.DuplicateData;
                    ReconStats.DistinctSaved = currentProgress.DistinctSaved;
                    ReconStats.DistinctData = currentProgress.DistinctData;
                    ProgressBarValue = (int)progressBarValue;
                    ProgressPercentage = percentageComplete;
                    RunTime = runTime;
                });
            });
        }
    }

}
