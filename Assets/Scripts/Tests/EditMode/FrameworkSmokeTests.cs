using NUnit.Framework;

public sealed class FrameworkSmokeTests
{
    [Test]
    public void NUnit_IsAvailable()
    {
        Assert.That(2 + 2, Is.EqualTo(4));
    }
}