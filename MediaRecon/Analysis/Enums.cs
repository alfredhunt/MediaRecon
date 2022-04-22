using ApexBytez.MediaRecon.Analysis;

namespace ApexBytez.MediaRecon.Analysis
{
    public enum DeleteStrategy
    {
        Recycle, // Recycle bin
        Delete, // Hard Delete
    }
    public enum MoveStrategy
    {
        Move,
        Copy
    }
    public enum SortingStrategy
    {
        None,
        YearAndMonth
    }
    public enum RunStrategy
    {
        Normal,
        DryRun
    }
}
