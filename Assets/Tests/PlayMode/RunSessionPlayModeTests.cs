using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RunSessionPlayModeTests
{
    [UnityTest]
    public IEnumerator Carry_IsConsumedExplicitly()
    {
        var session = RunSession.Current;
        session.ClearCarry();
        session.HasCarry = true;
        session.CarryCards["attack"] = 1;
        yield return null;

        Assert.That(session.HasCarry, Is.True);
        Assert.That(session.CarryCards["attack"], Is.EqualTo(1));
        session.ClearCarry();
        Assert.That(session.HasCarry, Is.False);
        Assert.That(session.CarryCards, Is.Empty);
    }
}
