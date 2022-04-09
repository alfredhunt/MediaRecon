namespace ApexBytez.MediaRecon.Events
{
    public class SourceFolderDragDropEvent
    {

        public SourceFolderDragDropEvent(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
