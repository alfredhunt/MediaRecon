using ApexBytez.MediaRecon.View;
using MethodTimer;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ApexBytez.MediaRecon.Extensions;
using System.Windows.Media.Imaging;
using static ApexBytez.MediaRecon.Analysis.ConflictedFiles;

namespace ApexBytez.MediaRecon.Analysis
{


    internal class ReconciledDirectory : ObservableObject, IFolderViewFolder
    {
        public string Name { get; private set; }
        public string FullName { get; protected set; }
        public DateTime LastWriteTime { get; private set; }
        public FolderViewItemType Type { get { return FolderViewItemType.Folder; } }

        public ObservableCollection<IFolderViewItem> Items { get; private set; } = new ObservableCollection<IFolderViewItem>();

        public ReconciledDirectory(string name)
        {
            Name = name;
        }

        public ReconciledDirectory(string name, ReconciledDirectory parent)
        {
            Name = name;
            Items.Add(parent);
        }

        public ReconciledDirectory(string name, ObservableCollection<IFolderViewItem> items)
        {
            Name = name;
            Items = items;
        }

        public void Add(IFolderViewItem item)
        {
            Items.Add(item);
            // TODO: Only valid for folders!
            switch (item.Type)
            {
                case FolderViewItemType.Folder:
                    // Listen to folder update so we can update our size
                    ((ReconciledDirectory)item).PropertyChanged += ReconciledDirectory_PropertyChanged;
                    break;
                case FolderViewItemType.File:
                    // Update our size
                    Size += item.Size;
                    break;
                default:
                    throw new ArgumentException("Invalid IFolderViewItem Type");
            }
        }

        private void ReconciledDirectory_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Size = Items.Sum(x => x.Size);
        }

        private long size;
        public long Size { get => size; set => SetProperty(ref size, value); }
    }

    internal enum ReconType
    {
        Distinct,
        Duplicate,
        AutoRename
    }

    internal abstract class ReconciledFile : IFolderViewItem
    {
        public ReconType ReconType { get; }
        public List<ReconFileInfo> Files { get; protected set; } = new List<ReconFileInfo>();
        public BitmapImage BitmapImage { get { return Files.First().BitmapImage; } }


        public string Name { get; protected set; }
        public string FullName { get; protected set; }
        public DateTime LastWriteTime { get; protected set; }
        public FolderViewItemType Type { get { return FolderViewItemType.File; } }
        public long Size { get; protected set; }
        public string ReconciliationDirectory { get; internal set; }
        public string ReconciledFilePath { get { return Path.Combine(ReconciliationDirectory, Name); } }

        public ReconciledFile(ReconType type)
        {
            Name = String.Empty;
            FullName = String.Empty;
            LastWriteTime = DateTime.Now;
            ReconType = type;
        }
    }

    internal class UniqueFile : ReconciledFile
    {
        public UniqueFile(FileInfo file) : base(ReconType.Distinct)
        {
            Files.Add(new ReconFileInfo(file));

            Name = file.Name;
            FullName = file.FullName;
            LastWriteTime = file.LastWriteTime;
            Size = file.Length;
        }

        public UniqueFile(FileInfo file, string name) : this(file)
        {
            Name = name;
        }
    }

    internal class DuplicateFiles : ReconciledFile
    {
        public long TotalFileCount { get; private set; }
        public long TotalFileSystemSize { get; private set; }
        public long DuplicateFileSystemSize { get; private set; }
        public long NumberOfDistinctFiles { get { return 1; } }
        public long NumberOfDuplicateFiles { get; private set; }

        public DuplicateFiles(IEnumerable<FileInfo> files) : base(ReconType.Duplicate)
        {
            Files = files.Select(x => new ReconFileInfo(x)).ToList();

            Name = files.First().Name;
            FullName = files.First().FullName;
            LastWriteTime = files.First().LastWriteTime;
            Size = files.First().Length;

            TotalFileCount = files.Count();
            TotalFileSystemSize = files.Sum(x => x.Length);

            DuplicateFileSystemSize = TotalFileSystemSize - Size;

            NumberOfDuplicateFiles = TotalFileCount - NumberOfDistinctFiles;
        }

        public DuplicateFiles(IEnumerable<FileInfo> files, string name) : this(files)
        {
            Name = name;
        }

    }

    internal class ConflictedFiles
    {
        public string Name { get; private set; }
        public List<ReconciledFile> ReconciledFiles { get; set; } = new List<ReconciledFile>();
        public string Description { get; set; }
        public long TotalFileCount { get; set; }
        public long TotalFileSystemSize { get; set; }
        public long DistinctFileSystemSize { get; set; }
        public long DuplicateFileSystemSize { get; set; }
        public long NumberOfDistinctFiles { get; set; }
        public long NumberOfDuplicateFiles { get; set; }        

        public ConflictedFiles(IEnumerable<IEnumerable<FileInfo>> files) 
        {
            Name = files.First().First().Name;
            
            var extension = Path.GetExtension(Name);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(Name);

            var count = 0;
            foreach (var group in files)
            {
                var reconFileName = string.Format("{0}-{1}{2}", nameWithoutExtension, count++, extension);
                                
                if (group.Count() == 1)
                {
                    var uniqueFile = new UniqueFile(group.First(), reconFileName);
                    ReconciledFiles.Add(uniqueFile);

                    TotalFileCount++;
                    TotalFileSystemSize += uniqueFile.Size; 
                    
                    NumberOfDistinctFiles++;
                    DistinctFileSystemSize += uniqueFile.Size;
                }
                else
                {
                    var duplicateFile = new DuplicateFiles(group, reconFileName);
                    ReconciledFiles.Add(duplicateFile);

                    Debug.Assert(duplicateFile.TotalFileCount == group.Count());
                    Debug.Assert(duplicateFile.TotalFileSystemSize == group.Sum(x => x.Length));

                    TotalFileCount += duplicateFile.TotalFileCount;
                    TotalFileSystemSize += duplicateFile.TotalFileSystemSize;

                    NumberOfDistinctFiles += duplicateFile.NumberOfDistinctFiles;
                    NumberOfDuplicateFiles += duplicateFile.NumberOfDuplicateFiles;
                    DistinctFileSystemSize += duplicateFile.Size;
                    DuplicateFileSystemSize += duplicateFile.DuplicateFileSystemSize;
                }
            }

            // File name: {1} Total Files, {2} Distinct Files
            Description = string.Format("{0}:  {1} Files [{4}],  {2} Duplicates [{5}], {3} Distinct [{6}]",
                Name, TotalFileCount, NumberOfDuplicateFiles, NumberOfDistinctFiles,
                TotalFileSystemSize.FormatFileSize(), DuplicateFileSystemSize.FormatFileSize(), DistinctFileSystemSize.FormatFileSize());
        }

        public ConflictedFiles(IEnumerable<IEnumerable<ReconFileInfo>> files) 
        {
            Name = files.First().First().FileInfo.Name;
            

            var extension = Path.GetExtension(Name);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(Name);

            foreach (var group in files)
            {
                var hash = group.First().Hash;
                var hasIdentifier = BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
                var reconFileName = string.Format("{0}-[{1}]{2}", nameWithoutExtension, hasIdentifier, extension);

                if (group.Count() == 1)
                {
                    var uniqueFile = new UniqueFile(group.First().FileInfo, reconFileName);
                    ReconciledFiles.Add(uniqueFile);

                    TotalFileCount++;
                    TotalFileSystemSize += uniqueFile.Size;

                    NumberOfDistinctFiles++;
                    DistinctFileSystemSize += uniqueFile.Size;
                }
                else
                {
                    var duplicateFile = new DuplicateFiles(group.Select(x => x.FileInfo), reconFileName);
                    ReconciledFiles.Add(duplicateFile);

                    //Debug.Assert(duplicateFile.TotalFileCount == group.Count());
                    //Debug.Assert(duplicateFile.TotalFileSystemSize == group.Sum(x => x.Length));

                    TotalFileCount += duplicateFile.TotalFileCount;
                    TotalFileSystemSize += duplicateFile.TotalFileSystemSize;

                    NumberOfDistinctFiles += duplicateFile.NumberOfDistinctFiles;
                    NumberOfDuplicateFiles += duplicateFile.NumberOfDuplicateFiles;
                    DistinctFileSystemSize += duplicateFile.Size;
                    DuplicateFileSystemSize += duplicateFile.DuplicateFileSystemSize;
                }
            }

            // File name: {1} Total Files, {2} Distinct Files
            Description = string.Format("{0}:  {1} Files [{4}],  {2} Duplicate [{5}], {3} Distinct [{6}]",
                Name, TotalFileCount, NumberOfDuplicateFiles, NumberOfDistinctFiles,
                TotalFileSystemSize.FormatFileSize(), DuplicateFileSystemSize.FormatFileSize(), DistinctFileSystemSize.FormatFileSize());
        }
    }

    

}
