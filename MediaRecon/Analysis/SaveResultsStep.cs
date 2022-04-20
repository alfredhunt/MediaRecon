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

namespace ApexBytez.MediaRecon.Analysis
{
    internal class SaveResultsStep : RunnableStep
    {
        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; }
        public ReconciliationStatistics ReconStats { get; set; } = new ReconciliationStatistics();

        private ReconciliationStatistics currentProgress = new ReconciliationStatistics();

        private object tplLock = new object();

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
                                ReconStats.SavedItems.Add(logMessage);
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
                                    ReconStats.SavedItems.Add(logMessage);
                                });
                            }
                        }
                        break;
                    case SortingStrategy.None:
                    default:
                        break;
                }

                // TODO: It might be nice from a ui perspective to see things being moved and delete
                // at the same time.  We would need to sort them and run two processes or at least
                // interleve them somehow...


                await Parallel.ForEachAsync(AnalysisResults.ReconciledFiles,
                      new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
                      async (file, ct) =>
                      {
                          // I want to do the work of copy/moving/deleting here...
                          //  BUT... we cant update UI all willy nilly here. We need to maybe use another observable subject
                          //  to streamline the events and update stats just like in the main analysis block
                          string message = string.Empty;

                          message = string.Format("Moving {0} from {1} to {2}",
                                  file.Name,
                                  file.FullName,
                                  file.ReconciledFilePath);
                          await Application.Current.Dispatcher.InvokeAsync(() =>
                          {
                              ReconStats.SavedItems.Add(message);
                          });

                          switch (AnalysisOptions.RunStrategy)
                          {
                              case RunStrategy.Normal:
                                  // TODO: We need to figure out a strat for when the destination has files in it
                                  try
                                  {
                                      switch (AnalysisOptions.MoveStrategy)
                                      {
                                          case MoveStrategy.Copy:
                                              File.Copy(file.FullName, file.ReconciledFilePath, false);
                                              break;
                                          case MoveStrategy.Move:
                                              File.Move(file.FullName, file.ReconciledFilePath, false);
                                              break;
                                      }
                                  }
                                  catch (Exception ex)
                                  {
                                      Debug.WriteLine(ex.ToString());
                                  }
                                  break;
                              case RunStrategy.DryRun:
                                  // Simulate some cost of copying or moving
                                  await Task.Delay(Properties.Settings.Default.DryRunDelay, ct);
                                  break;
                              default:
                                  // Shouldn't ever get here.
                                  break;
                          }
                          lock (tplLock)
                          {
                              currentProgress.FilesProcessed++;
                              currentProgress.DataProcessed += file.Size;
                              currentProgress.DistinctSaved++;
                              currentProgress.DistinctData += file.Size;
                          }
                          switch (file.ReconType)
                          {
                              case ReconType.Distinct:
                                  // This case is take care of by the logic above because each duplicate set
                                  //    has a distinct file in it intrinsically 
                                  break;
                              case ReconType.Duplicate:
                                  var duplicate = file as DuplicateFiles;

                                  foreach (var item in duplicate.Files.Skip(1))
                                  {
                                      // TODO: do we want or need to produce a list of all files remove?
                                      // Might be a good idea
                                      message = string.Format("Removing {0}", item.FullName);
                                      await Application.Current.Dispatcher.InvokeAsync(() =>
                                      {
                                          ReconStats.RemovedItems.Add(message);
                                      });

                                      switch (AnalysisOptions.RunStrategy)
                                      {
                                          case RunStrategy.Normal:
                                              try 
                                              {
                                                  switch (AnalysisOptions.DeleteStrategy)
                                                  {
                                                      case DeleteStrategy.Soft:
                                                          if (!FileOperationAPIWrapper.MoveToRecycleBin(item.FullName))
                                                          {
                                                              Debug.WriteLine("Failed to recycle {0}", item.FullName); ;
                                                          }
                                                          break;
                                                      case DeleteStrategy.Hard:
                                                          File.Delete(item.FullName);
                                                          break;
                                                  }
                                              }
                                              catch (Exception ex)
                                              {
                                                  Debug.WriteLine(ex.ToString());
                                              }
                                              break;
                                          case RunStrategy.DryRun:
                                              // Simulate some cost of copying or moving
                                              await Task.Delay(Properties.Settings.Default.DryRunDelay, ct);
                                              break;
                                          default:
                                              // Shouldn't ever get here.
                                              break;
                                      }

                                      lock (tplLock)
                                      {
                                          currentProgress.FilesProcessed++;
                                          currentProgress.DataProcessed += item.Length;
                                          currentProgress.DuplicatesDeleted++;
                                          currentProgress.DuplicateData += item.Length;
                                      }
                                  }
                                  break;
                          }
                      });
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
                Debug.Assert(false);
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
