using MediaRecon;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ApexBytez.MediaRecon.Events
{
    public class SourceFolderDeleteEvent : GenericEvent<int>
    {
        public SourceFolderDeleteEvent(int index) : base(index) { }
    }
}
