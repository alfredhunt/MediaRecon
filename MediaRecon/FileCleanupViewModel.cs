using System.Collections.Generic;
using System.IO;

namespace MediaRecon
{
    public class FileCleanupViewModel
    {
        private KeyValuePair<string, List<FileInfo>> _fileInfo = new KeyValuePair<string, List<FileInfo>>();

        public FileCleanupViewModel()
        { 
        }

        public FileCleanupViewModel(KeyValuePair<string, List<FileInfo>> fileInfo)
        {
            _fileInfo = fileInfo;
        }
    }
}
