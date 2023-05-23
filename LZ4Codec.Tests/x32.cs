using LZ4;
using NUnit.Framework;

namespace LZ4Codec.Tests;

public class x32 : BaseTest
{
    [OneTimeSetUp]
    public override void OneTimeSetup()
    {
        base.OneTimeSetup();
        service = new LZ4Service32();
    }
}