﻿using ApexBytez.MediaRecon.View;
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
        private Subject<KeyValuePair<ProcessedType, long>> subject;
        private IDisposable subjectDisposable;
        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; }
        public ReconciliationStatistics ReconStats { get; set; } = new ReconciliationStatistics();

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

                // Can we use FileInfo here? for things that are saved we can get updated, delete we use old
                // and we can also pass some status to show saved/delete...  Might work
                subject = new Subject<KeyValuePair<ProcessedType, long>>();
                subjectDisposable = subject.Buffer(TimeSpan.FromMilliseconds(24))
                    .Where(x => x.Count > 0)
                    .Subscribe(x =>
                    {
                        ProcessReconciledFiles(x);
                    });

                await Parallel.ForEachAsync(AnalysisResults.ReconciledFiles,
                      new ParallelOptions { MaxDegreeOfParallelism = 32, CancellationToken = cancellationToken },
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
                          await Application.Current.Dispatcher.BeginInvoke(() =>
                          {
                              ReconStats.SavedItems.Add(message);
                          });

                          switch (AnalysisOptions.RunStrategy)
                          {
                              case RunStrategy.Normal:
                                  // TODO: We need to figure out a strat for when the destination has files in it
                                  try
                                  {
                                      File.Copy(file.FullName, file.ReconciledFilePath, false);
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

                          subject.OnNext(new KeyValuePair<ProcessedType, long>(ProcessedType.Distinct, file.Size));

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
                                      await Application.Current.Dispatcher.BeginInvoke(() =>
                                      {
                                          ReconStats.RemovedItems.Add(message);
                                      });

                                      switch (AnalysisOptions.RunStrategy)
                                      {
                                          case RunStrategy.Normal:

                                              // TODO: Could use a different type of observable that accepts each file
                                              //    plus a flag denoting unique/save/moved and duplicate/recycled/deleted
                                              FileOperationAPIWrapper.MoveToRecycleBin(item.FullName);

                                              break;
                                          case RunStrategy.DryRun:
                                              // Simulate some cost of copying or moving
                                              await Task.Delay(Properties.Settings.Default.DryRunDelay, ct);
                                              break;
                                          default:
                                              // Shouldn't ever get here.
                                              break;
                                      }

                                      subject.OnNext(new KeyValuePair<ProcessedType, long>(ProcessedType.Duplicate, item.Length));
                                  }
                                  break;
                          }
                      });
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        internal enum ProcessedType
        {
            Distinct,
            Duplicate
        }

        private void ProcessReconciledFiles(IList<KeyValuePair<ProcessedType, long>> processedFiles)
        {
            long filesProcessed = ReconStats.FilesProcessed;
            long dataProcessed = ReconStats.DataProcessed;

            long duplicatesDeleted = ReconStats.DuplicatesDeleted;
            long duplicateData = ReconStats.DuplicateData;

            long distinctSaved = ReconStats.DistinctSaved;
            long distinctData = ReconStats.DistinctData;

            foreach (var file in processedFiles)
            {
                filesProcessed++;
                dataProcessed += file.Value;

                switch (file.Key)
                {
                    case ProcessedType.Distinct:
                        distinctSaved++;
                        distinctData += file.Value;
                        break;
                    case ProcessedType.Duplicate:
                        duplicatesDeleted++;
                        duplicateData += file.Value;
                        break;
                }
            }

            var progressRatio = ((double)filesProcessed / AnalysisResults.FileCount);
            var percentageComplete = progressRatio * 100;
            var progressBarValue = progressRatio * Properties.Settings.Default.ProgressBarMaximum;
            Application.Current.Dispatcher.Invoke(() =>
            {
                ReconStats.FilesProcessed = filesProcessed;
                ReconStats.DataProcessed = dataProcessed;
                ReconStats.DuplicatesDeleted = duplicatesDeleted;
                ReconStats.DuplicateData = duplicateData;
                ReconStats.DistinctSaved = distinctSaved;
                ReconStats.DistinctData = distinctData;
                ProgressBarValue = (int)progressBarValue;
                ProgressPercentage = percentageComplete;
            });

        }
    }
}
