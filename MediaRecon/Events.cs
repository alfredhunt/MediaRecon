using MediaRecon;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MediaRecon.Events
{
    public class DialogResults<T>
    {
        public enum Results
        {
            Ok,
            Cancel
        }
        public Results Result { get; private set; }
        public T GetT { get; private set; }
        DialogResults(Results result, T t)
        {
            Result = result;
            GetT = t;
        }
    }

    public class SystemFolderUserRequest : RequestMessage<DialogResults<DirectoryInfo>>
    { }
    
    public abstract class GenericEvent<T>
    {
        public T Data { get; private set; }

        public GenericEvent(T data) { Data = data; }
    }

    public class SourceFolderDragDropEvent
    {

        public SourceFolderDragDropEvent(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }

    public class SourceFolderDeleteEvent : GenericEvent<int>
    {
        public SourceFolderDeleteEvent(int index) : base(index) { }
    }
}
