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

namespace ApexBytez.MediaRecon.Analysis
{
    internal class SaveResults : RunnableStep
    {
        private Subject<ReconciledFile> subject;
        private IDisposable subjectDisposable;

        public AnalysisOptions AnalysisOptions { get; private set; }
        public AnalysisResults AnalysisResults { get; private set; }
        public ReconciliationStatistics ReconStats { get; set; } = new ReconciliationStatistics();

        public SaveResults(AnalysisOptions analysisOptions, AnalysisResults analysisResults)
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
                subject = new Subject<ReconciledFile>();
                subjectDisposable = subject.Buffer(TimeSpan.FromMilliseconds(24))
                    .Where(x => x.Count > 0)
                    .Subscribe(x =>
                    {
                        ProcessReconciledFiles(x);
                    });

                await Parallel.ForEachAsync(AnalysisResults.ReconciledFiles,
                      new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = cancellationToken },
                      async (file, ct) =>
                      {
                          // I want to do the work of copy/moving/deleting here...
                          //  BUT... we cant update UI all willy nilly here. We need to maybe use another observable subject
                          //  to streamline the events and update stats just like in the main analysis block
                          string message = string.Empty;

                          // TODO: We need to figure out a strat for when the destination has files in it
                          try
                          {
                              File.Copy(file.FullName, file.ReconciledFilePath, false);
                          }
                          catch (Exception ex)
                          {
                              Debug.WriteLine(ex.ToString());
                          }

                          message = string.Format("Moved {0} from {1} to {2}",
                                  file.Name,
                                  file.FullName,
                                  file.ReconciledFilePath);
                          Application.Current.Dispatcher.Invoke(() =>
                          {
                              ReconStats.SavedItems.Add(message);
                          });


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
                                      FileOperationAPIWrapper.MoveToRecycleBin(item.FullName);
                                  }
                                  message = string.Format("Removing {0} duplicate(s) of {1}",
                                      duplicate.NumberOfDuplicateFiles,
                                      duplicate.Name);
                                  Application.Current.Dispatcher.Invoke(() =>
                                  {
                                      ReconStats.RemovedItems.Add(message);
                                  });
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
        private void ProcessReconciledFiles(IList<ReconciledFile> x)
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

            var progressBarValue = ((double)filesProcessed / AnalysisResults.FileCount) * Properties.Settings.Default.ProgressBarMaximum;
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
        }
    }
}
