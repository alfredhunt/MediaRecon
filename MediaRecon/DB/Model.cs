using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ApexBytez.MediaRecon.DB
{
    public class MediaReconContext : DbContext
    {
        public DbSet<Directory> Directories { get; set; }
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
    }

    public class Options
    {

    }

    public class Results
    {

    }

    public class FileSystem
    {
        public string FullName { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public string Name { get; set; }
    }

    public class Directory : FileSystem
    {
        public int DirectoryId { get; set; }
        public List<File> Files { get; set; }
    }

    public class File : FileSystem
    {
        public int FileId { get; set; }
        public string DirectoryName { get; set; }
        public long Length { get; set; }
        public byte[] Hash { get; set; }
        public string HashAlgorithm { get; set; }
    }
}
