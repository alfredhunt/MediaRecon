using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ApexBytez.MediaRecon.DB
{
    public class MediaReconContext : DbContext
    {
        public DbSet<File> Files { get; set; }
        public string DbPath { get; }

        public MediaReconContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = System.IO.Path.Join(path, "MediaRecon", "mediarecon.db");
        }

        // The following configures EF to create a Sqlite database file in the
        // special "local" folder for your platform.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");
    }

    public class Analysis
    {
        // TODO: definitely want to build this out so we can show statistics over time for the 
        //  user and how much data they freed up.
        public long FilesAnalayzed { get; set; }
        public long DataAnalyzed { get; set; }
        public long DuplicateFilesRemoved { get; set; }
        public long SpaceSaved { get; set; }
    }

    public class FileSystem
    {
        public string FullName { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string Name { get; set; }
    }

    public class File : FileSystem
    {
        public int FileId { get; set; }
        public string DirectoryName { get; set; }
        public long Length { get; set; }
        public byte[] Hash { get; set; }
        public string HashAlgorithm { get; set; }
    }


    public class Database
    {
        public DB.File? GetDBFileInfo(FileInfo fileInfo)
        {
            //
            // https://docs.microsoft.com/en-us/ef/core/get-started/overview/first-app?tabs=visual-studio
            //
            using (var db = new MediaReconContext())
            {
                // Does it exist?
                return db.Files.FirstOrDefault(x =>
                    x.FullName.Equals(fileInfo.FullName) &&
                    x.Length == fileInfo.Length &&
                    x.LastWriteTime == fileInfo.LastWriteTime);
            }
        }

        public DB.File AddDBFileInfo(FileInfo fileInfo, byte[] hash)
        {
            var file = new DB.File
            {
                CreationTime = fileInfo.CreationTime,
                DirectoryName = fileInfo.DirectoryName,
                FullName = fileInfo.FullName,
                Hash = hash,
                HashAlgorithm = "SHA256",
                LastAccessTime = fileInfo.LastAccessTime,
                LastWriteTime = fileInfo.LastWriteTime,
                Length = fileInfo.Length,
                Name = fileInfo.Name,
            };

            using (var db = new MediaReconContext())
            {
                // Note: This sample requires the database to be created before running.
                Console.WriteLine($"Database path: {db.DbPath}.");
                db.Add(file);
                db.SaveChanges();
            }

            return file;
        }

        public DB.File UpdateDBFileInfo(FileInfo oldInfo, FileInfo newInfo)
        {
            DB.File? file = GetDBFileInfo(oldInfo);
            if (file == null)
            {
                file.CreationTime = newInfo.CreationTime;
                file.DirectoryName = newInfo.DirectoryName;
                file.FullName = newInfo.FullName;
                file.LastAccessTime = newInfo.LastAccessTime;
                file.LastWriteTime = newInfo.LastWriteTime;
                file.Length = newInfo.Length;
                file.Name = newInfo.Name;

                using (var db = new MediaReconContext())
                {
                    // Note: This sample requires the database to be created before running.
                    Console.WriteLine($"Database path: {db.DbPath}.");
                    db.Update(file);
                    db.SaveChanges();
                }
            }
            else
            {
                Debug.Assert(true);
            }

            return file;
        }
    }

}
    