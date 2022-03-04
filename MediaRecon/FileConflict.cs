using ApexBytez.MediaRecon.View;
using MethodTimer;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace ApexBytez.MediaRecon
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

    internal abstract class FileRecon
    {
        public ReconType ReconType { get; }

        public FileRecon(ReconType type)
        {
            ReconType = type;
        }
    }

    internal abstract class ReconciledFile : FileRecon, IFolderViewItem
    {
        public string Name { get; protected set; }
        public string FullName { get; protected set; }
        public DateTime LastWriteTime { get; protected set; }
        public FolderViewItemType Type { get { return FolderViewItemType.File; } }
        public long Size { get; protected set; }        

        public ReconciledFile(ReconType type) : base(type) 
        {
            Name = String.Empty;
            FullName = String.Empty;
            LastWriteTime = DateTime.Now;
        }
    }

    internal class UniqueFile : ReconciledFile
    {
        public FileInfo File { get; set; }

        public UniqueFile(FileInfo file) : base(ReconType.Distinct)
        {
            File = file;

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
        public IEnumerable<FileInfo> Files { get; private set; }
        public long TotalFileCount { get; private set; }
        public long TotalFileSystemSize { get; private set; }
        public long DuplicateFileSystemSize { get; private set; }
        public long NumberOfDistinctFiles { get { return 1; } }
        public long NumberOfDuplicateFiles { get; private set; }

        public DuplicateFiles(IEnumerable<FileInfo> files) : base(ReconType.Duplicate)
        {
            Files = files;

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

    internal class ConflictedFiles : FileRecon
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

        public ConflictedFiles(IEnumerable<IEnumerable<FileInfo>> files) : base(ReconType.AutoRename)
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
    }

}
