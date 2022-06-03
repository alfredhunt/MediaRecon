using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace ApexBytez.MediaRecon.Analysis
{
    /// <summary>
    /// Wraps FileInfo and provides a few helpers
    /// </summary>
    internal class ReconFileInfo
    {
        public FileInfo FileInfo { get; set; }
        public string Name { get { return FileInfo.Name; } }
        public string FullName { get { return FileInfo.FullName; } }
        public DateTime LastWriteTime { get { return FileInfo.LastWriteTime; } }
        public long Length { get { return FileInfo.Length; } }

        /// <summary>
        /// Used in grouping potentially conflictd file names and duplicates
        /// </summary>
        public byte[] Hash { get; set; }
        public BitmapImage BitmapImage { get { return LoadBitmapImage(); } }


        public ReconFileInfo(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
        }
        public ReconFileInfo(FileInfo fileInfo, byte[] hash)
            : this(fileInfo)
        {
            Hash = hash;
        }
        /// <summary>
        /// Need a way to load the file in without keeping the file busy. This is that attempt
        /// </summary>
        /// <returns></returns>
        private BitmapImage LoadBitmapImage()
        {
            var uri = new Uri(FileInfo.FullName);
            var bitmap = new BitmapImage(uri);
            return bitmap;
        }
    }

}
