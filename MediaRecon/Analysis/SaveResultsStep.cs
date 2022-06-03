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

        private Subject<Tuple<Reconciled, string>> messageSubject = new();

        public SaveResultsStep(AnalysisOptions analysisOptions, AnalysisResults analysisResults)
        {
            AnalysisOptions = analysisOptions;
            AnalysisResults = analysisResults;
        }

        private enum Reconciled
        {
            Saved,
            Removed
        }

        protected override async Task RunAsyncStep()
        {
            string formatFolderExist = "Directory Exists: {0}";
            string formatFolderCreated = "Created Directory: {0}";
            string logMessage = string.Empty;
            try
            {
                var previous = DateTime.Now;
                var messageDisposable = messageSubject
                    .Buffer(TimeSpan.FromMilliseconds(150))
                    .Where(x => x.Any())
                    .Subscribe(async x =>
                    {
                        var now = DateTime.Now;
                        var delta = (now - previous).TotalMilliseconds;
                        previous = now;

                        foreach (var z in x)
                        {
                            switch (z.Item1)
                            {
                                case Reconciled.Saved:
                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        if (ReconStats.SavedItems.Count > 200)
                                        {
                                            ReconStats.SavedItems.RemoveAt(0);
                                        }
                                        ReconStats.SavedItems.Add(z.Item2);
                                    });
                                    break;
                                case Reconciled.Removed:
                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        if (ReconStats.RemovedItems.Count > 200)
                                        {
                                            ReconStats.RemovedItems.RemoveAt(0);
                                        }
                                        ReconStats.RemovedItems.Add(z.Item2);
                                    });
                                    break;
                            }
                        }
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
                                messageSubject.OnNext(new Tuple<Reconciled, string>(Reconciled.Saved, logMessage));
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
                                    messageSubject.OnNext(new Tuple<Reconciled, string>(Reconciled.Saved, logMessage));
                                });
                            }
                        }
                        break;
                    case SortingStrategy.None:
                    default:
                        break;
                }

                await Parallel.ForEachAsync(AnalysisResults.ReconciledFiles,
                      new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken },
                      async (file, ct) =>
                      {
                          try
                          {
                              // Do any of the files already exist in the destination? Keep it, delete the others
                              var destinationFile = file.Files.FirstOrDefault(x => x.FileInfo.FullName == file.ReconciledFilePath);

                              if (destinationFile != null)
                              {
                                  // The file already exists in the destination
                                  // Remove the other copies
                                  var message = string.Format("Unique file already exists in destination: {0}", file.ReconciledFilePath);
                                  //Debug.WriteLine();
                                  messageSubject.OnNext(new Tuple<Reconciled, string>(Reconciled.Saved, message));
                                                                    
                                  file.Files.Remove(destinationFile);
                                  await Task.Delay(Properties.Settings.Default.DryRunDelay, cancellationToken);
                                  lock (tplLock)
                                  {
                                      currentProgress.FilesProcessed++;
                                      currentProgress.DataProcessed += destinationFile.FileInfo.Length;
                                      currentProgress.DistinctSaved++;
                                      currentProgress.DistinctData += destinationFile.FileInfo.Length;
                                  }

                                  // Recycle/Delete the rest
                                  foreach (var item in file.Files)
                                  {
                                      await RecycleOrDeleteAsync(item.FileInfo, cancellationToken);
                                  }
                              }
                              else
                              {
                                  // Copy will have the effect of copying one, removing all others
                                  //    This effect will be at random and probably doesn't make 
                                  //    much sense to have a copy strategy at all.

                                  // Move/Copy One
                                  await MoveOrCopyAsync(file.Files.First().FileInfo, file.ReconciledFilePath, cancellationToken);

                                  // Recycle/Delete the rest
                                  foreach (var item in file.Files.Skip(1))
                                  {
                                      // TODO: do we want or need to produce a list of all files remove?
                                      // Might be a good idea
                                      await RecycleOrDeleteAsync(item.FileInfo, cancellationToken);
                                  }
                              }
                          }
                          catch (Exception ex)
                          {
                              Debug.WriteLine("Parallel.ForEachAsync: {0}", ex);
                          }
                      });

                //await Task.Delay(TimeSpan.FromSeconds(1));
                messageDisposable.Dispose();
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                await UpdateProgress();

                Database database = new Database();
                DB.RunStatistics? stats = await database.AddRunStatistics(
                    DateTime.Now,
                    AnalysisResults.NumberOfFilesAnalyzed,
                    AnalysisResults.SizeOfDataAnalyzed,
                    AnalysisResults.NumberOfDuplicateFiles,
                    ReconStats.DuplicatesDeleted,
                    ReconStats.DuplicateData
                    );
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
                            await Task.Run(() =>
                            {
                                if (!FileOperationAPIWrapper.MoveToRecycleBin(fileInfo.FullName))
                                {
                                    Debug.WriteLine("Failed to recycle {0}", fileInfo.FullName);
                                }
                                Debug.Assert(!System.IO.File.Exists(fileInfo.FullName));
                            });
                            break;
                        case DeleteStrategy.Delete:
                            await Task.Run(() => fileInfo.Delete());
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

            lock (tplLock)
            {
                currentProgress.FilesProcessed++;
                currentProgress.DataProcessed += fileInfo.Length;
                currentProgress.DuplicatesDeleted++;
                currentProgress.DuplicateData += fileInfo.Length;
            }

            messageSubject.OnNext(new Tuple<Reconciled, string>(Reconciled.Removed, message));
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
                        Debug.Assert(!System.IO.File.Exists(destination));
                        switch (AnalysisOptions.MoveStrategy)
                        {
                            case MoveStrategy.Copy:
                                await Task.Run(() => fileInfo.Copy(destination, false));
                                break;
                            case MoveStrategy.Move:
                                await Task.Run(() => fileInfo.Move(destination, false));
                                break;
                        }

                        Database database = new Database();
                        await database.UpdateDBFileInfoAsync(fileInfo, new FileInfo(destination));

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

            lock (tplLock)
            {
                currentProgress.FilesProcessed++;
                currentProgress.DataProcessed += fileInfo.Length;
                currentProgress.DistinctSaved++;
                currentProgress.DistinctData += fileInfo.Length;
            }

            messageSubject.OnNext(new Tuple<Reconciled, string>(Reconciled.Saved, message));
        }

        protected override Task UpdateProgress()
        {
            return Task.Run(async () =>
            {
                var progressRatio = ((double)currentProgress.FilesProcessed / AnalysisResults.NumberOfFilesInAnalysis);
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
