namespace LZ4;

public static class Consts
{
    /// <summary>
    /// Memory usage formula : N->2^N Bytes (examples : 10 -> 1KB; 12 -> 4KB ; 16 -> 64KB; 20 -> 1MB; etc.)
    /// Increasing memory usage improves compression ratio
    /// Reduced memory usage can improve speed, due to cache effect
    /// Default value is 14, for 16KB, which nicely fits into Intel x86 L1 cache
    /// </summary>
    public const int MEMORY_USAGE = 14;

    public const int COPYLENGTH = 8;

    public const int MFLIMIT = COPYLENGTH + MINMATCH;

    public const int MINMATCH     = 4;
    public const int LASTLITERALS = 5;
    public const int MINLENGTH    = MFLIMIT + 1;

    public const int ML_BITS  = 4;
    public const int ML_MASK  = (1 << ML_BITS)  - 1;
    public const int RUN_BITS = 8               - ML_BITS;
    public const int RUN_MASK = (1 << RUN_BITS) - 1;

    public const uint MULTIPLIER = 2654435761u;

    public const int LZ4_64KLIMIT = (1 << 16) + (MFLIMIT - 1);
}

public static class Consts32
{
    public const int HASH_LOG       = Consts.MEMORY_USAGE - 2;
    public const int HASH_TABLESIZE = 1 << HASH_LOG;
    public const int HASH_ADJUST    = Consts.MINMATCH * 8 - HASH_LOG;

    public const int STEPSIZE = 4;
}

public static class Consts64
{
    public const int HASH_LOG       = Consts32.HASH_LOG + 1;
    public const int HASH_TABLESIZE = 1 << HASH_LOG;
    public const int HASH_ADJUST    = Consts.MINMATCH * 8 - HASH_LOG;

    public const int STEPSIZE = 8;
}