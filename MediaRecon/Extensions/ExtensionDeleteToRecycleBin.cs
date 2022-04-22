using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexBytez.MediaRecon.Extensions
{
    /// <summary>  
    /// ExtensionDeleteToRecycleBin.cs  
    /// https://www.c-sharpcorner.com/blogs/extension-methods-for-delete-files-and-folders-to-recycle-bin#:~:text=Sadly%2C%20C%23%20doesn%27t%20have%20a%20native%20API%20to,named%20ExtensionDeleteToRecycleBin.cs%20or%20other%20name%20if%20you%20like.
    /// </summary>  
    public static class ExtensionDeleteToRecycleBin
    {

        /// <summary>  
        /// Delete File To Recycle Bin  
        /// WARMING: NETWORK FILES DON'T GO TO RECYCLE BIN  
        /// </summary>  
        /// <param name="file"></param>  
        public static void FileRecycle(this string file)
            =>
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file,
            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

        /// <summary>  
        /// Delete Path To Recycle Bin  
        /// WARMING: NETWORK PATHS DON'T GO TO RECYCLE BIN  
        /// </summary>  
        /// <param name="path"></param>  
        public static void DirectoryRecycle(this string path)
            =>
        Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

        public static void Recycle(this FileInfo fileInfo)
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(fileInfo.FullName,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
    }
}
