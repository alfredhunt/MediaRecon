using MethodTimer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ApexBytez.MediaRecon
{
    internal class FileAnalysis
    {
        [Time]
        public static SortedList<string, List<FileInfo>> GetSortedFileInfo(IEnumerable<string> directories)
        {
            var sortedList = new SortedList<string, List<FileInfo>>();
            foreach (string directory in directories)
            {
                GetSortedFileInfo(directory, sortedList);
            }
            return sortedList;
        }

        public static void GetSortedFileInfo(string directory, SortedList<string, List<FileInfo>> sortedList)
        {
            GetSortedFileInfo(new DirectoryInfo(directory), sortedList);
        }

        public static void GetSortedFileInfo(DirectoryInfo directoryInfo, SortedList<string, List<FileInfo>> sortedList)
        {
            // Add this directories files 
            foreach (var file in directoryInfo.EnumerateFiles())
            {
                if (sortedList.ContainsKey(file.Name))
                {
                    sortedList[file.Name].Add(file);
                }
                else
                {
                    sortedList.Add(file.Name, new List<FileInfo>() { file });
                }
            }
            // Then recurse directories
            foreach (DirectoryInfo dirInfo in directoryInfo.GetDirectories())
            {
                GetSortedFileInfo(dirInfo, sortedList);
            }
        }
        [Time]
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, List<FileInfo>>> GetYearMonthSortedFileInfo(IEnumerable<string> directories)
        {
            ConcurrentDictionary<string, ConcurrentDictionary<string, List<FileInfo>>> concurrentList = new();
            foreach (string directory in directories)
            {
                GetYearMonthSortedFileInfo(new DirectoryInfo(directory), concurrentList);
            }
            return concurrentList;
        }

        public static void GetYearMonthSortedFileInfo(DirectoryInfo directoryInfo, ConcurrentDictionary<string, ConcurrentDictionary<string, List<FileInfo>>> concurrentList)
        {
            // For each file in directory
            foreach (var file in directoryInfo.EnumerateFiles())
            {
                //var year = file.LastWriteTime.Year;
                //var month = file.LastWriteTime.Month;
                var yearMonthKey = file.LastWriteTime.ToString("MM/yyyy");

                var yearMonthDictionary = concurrentList.GetOrAdd(yearMonthKey, new ConcurrentDictionary<string, List<FileInfo>>());

                var fileNameList = yearMonthDictionary.GetOrAdd(file.Name, new List<FileInfo>());

                fileNameList.Add(file);
            }
            // Then recurse directories
            foreach (DirectoryInfo dirInfo in directoryInfo.GetDirectories())
            {
                GetYearMonthSortedFileInfo(dirInfo, concurrentList);
            }
        }


        public static List<FileInfo> GetFileInfo(IEnumerable<string> directories)
        {
            var fileList = new List<FileInfo>();
            foreach (string directory in directories)
            {
                fileList.AddRange(GetFileInfo(directory));
            }
            return fileList;
        }

        public static List<FileInfo> GetFileInfo(string directory)
        {
            return GetFileInfo(new DirectoryInfo(directory));
        }

        public static List<FileInfo> GetFileInfo(DirectoryInfo directoryInfo)
        {
            var fileList = directoryInfo.GetFiles().ToList();

            foreach (DirectoryInfo dirInfo in directoryInfo.GetDirectories())
            {
                fileList.AddRange(GetFileInfo(dirInfo));
            }
            return fileList;
        }

        public static bool FileCompare(FileInfo file1, FileInfo file2)
        {
            System.Diagnostics.Debug.Assert(file1.Length == file2.Length);
            return FileCompare(file1.FullName, file2.FullName);
        }

        /// <summary>
        /// This method accepts two strings the represent two files to
        // compare. A return value of 0 indicates that the contents of the files
        // are the same. A return value of any other value indicates that the
        // files are not the same.
        /// https://docs.microsoft.com/en-us/troubleshoot/developer/visualstudio/csharp/general/create-file-compare
        /// </summary>
        //[Time]
        public static bool FileCompare(string file1, string file2)
        {
            int file1byte;
            int file2byte;
            FileStream fs1;
            FileStream fs2;

            // Determine if the same file was referenced two times.
            if (file1 == file2)
            {
                // Return true to indicate that the files are the same.
                return true;
            }

            // Open the two files.
            fs1 = new FileStream(file1, FileMode.Open);
            fs2 = new FileStream(file2, FileMode.Open);

            // Check the file sizes. If they are not the same, the files
            // are not the same.
            if (fs1.Length != fs2.Length)
            {
                // Close the file
                fs1.Close();
                fs2.Close();

                // Return false to indicate files are different
                return false;
            }

            // Read and compare a byte from each file until either a
            // non-matching set of bytes is found or until the end of
            // file1 is reached.
            do
            {
                // Read one byte from each file.
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));

            // Close the files.
            fs1.Close();
            fs2.Close();

            // Return the success of the comparison. "file1byte" is
            // equal to "file2byte" at this point only if the files are
            // the same.
            return ((file1byte - file2byte) == 0);
        }

        const int BYTES_TO_READ = sizeof(Int64);

        //[Time]
        public static bool FilesAreEqual(FileInfo first, FileInfo second)
        {
            if (first.Length != second.Length)
                return false;

            if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
                return true;

            int iterations = (int)Math.Ceiling((double)first.Length / BYTES_TO_READ);

            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] one = new byte[BYTES_TO_READ];
                byte[] two = new byte[BYTES_TO_READ];

                for (int i = 0; i < iterations; i++)
                {
                    fs1.Read(one, 0, BYTES_TO_READ);
                    fs2.Read(two, 0, BYTES_TO_READ);

                    if (BitConverter.ToInt64(one, 0) != BitConverter.ToInt64(two, 0))
                        return false;
                }
            }

            return true;
        }

        //[Time]
        public static bool FilesAreEqual_Hash(FileInfo first, FileInfo second)
        {
            using (FileStream fs1 = first.OpenRead())
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] firstHash = MD5.Create().ComputeHash(fs1);
                byte[] secondHash = MD5.Create().ComputeHash(fs2);

                for (int i = 0; i < firstHash.Length; i++)
                {
                    if (firstHash[i] != secondHash[i])
                        return false;
                }
                return true;
            }
        }

        //[Time]
        public static bool FilesAreEqual_Hash2(byte[] firstHash, FileInfo second)
        {
            using (FileStream fs2 = second.OpenRead())
            {
                byte[] secondHash = MD5.Create().ComputeHash(fs2);

                for (int i = 0; i < firstHash.Length; i++)
                {
                    if (firstHash[i] != secondHash[i])
                        return false;
                }
                return true;
            }
        }

        public static async Task<bool> FilesAreEqualAsync(byte[] firstHash, FileInfo second, CancellationToken cancellationToken)
        {
            using (FileStream fs2 = second.OpenRead())
            {
                return await MD5.Create()
                    .ComputeHashAsync(fs2, cancellationToken)
                    .ContinueWith(secondHash =>
                    {
                        for (int i = 0; i < firstHash.Length; i++)
                        {
                            if (firstHash[i] != secondHash.Result[i])
                                return false;
                        }
                        return true;
                    });
            }
        }
    }
}
