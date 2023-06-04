namespace LZ4.Helpers;

readonly struct LastLiteralsEncode
{
    public readonly int SourceLiterals;
    public readonly int SourceLiterals1;
    public readonly int SourceLiterals3;
    public readonly int SourceStepSize1;
    public readonly int DestLiterals1;
    public readonly int DestLiterals3;

    public LastLiteralsEncode(int LASTLITERALS, int STEPSIZE, int src_end, int dst_end)
    {
        SourceLiterals  = src_end        - LASTLITERALS;
        SourceLiterals1 = SourceLiterals - 1;
        SourceLiterals3 = SourceLiterals - 3;
        SourceStepSize1 = SourceLiterals - (STEPSIZE - 1);

        DestLiterals1 = dst_end - (1 + LASTLITERALS);
        DestLiterals3 = dst_end - (2 + 1 + LASTLITERALS);
    }
}