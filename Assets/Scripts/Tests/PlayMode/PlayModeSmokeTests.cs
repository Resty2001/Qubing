using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class PlayModeSmokeTests
{
    [UnityTest]
    public IEnumerator UnityPlayerLoop_AdvancesOneFrame()
    {
        yield return null;

        Assert.Pass();
    }
}