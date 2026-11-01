using NUnit.Framework;
using UnityEngine;

public class StageMapTests
{
    GameObject go;
    StageMap map;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("StageMapTest");
        map = go.AddComponent<StageMap>();
        map.mapSize = new Vector2Int(120, 20);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(go);

    [Test]
    public void EncounterBoundaries_AreStoredOnMapData()
    {
        map.horizontalGatePositions = new[] { 30f, 60f, 90f };
        map.verticalGateHeights = new[] { 20f, 40f };
        Assert.That(map.horizontalGatePositions, Is.EqualTo(new[] { 30f, 60f, 90f }));
        Assert.That(map.verticalGateHeights, Is.EqualTo(new[] { 20f, 40f }));
    }

    [Test]
    public void VerticalBarrier_BlocksOnlyHorizontalStage()
    {
        map.verticalLayout = false;
        map.AddVerticalBarrier(30f);
        Assert.That(map.IsSolid(new Vector2(30f, 5f)), Is.True);
        map.RemoveVerticalBarrier(30f);
        Assert.That(map.IsSolid(new Vector2(30f, 5f)), Is.False);
    }

    [Test]
    public void HorizontalBarrier_BlocksOnlyVerticalStage()
    {
        map.verticalLayout = true;
        map.AddHorizontalBarrier(10f);
        Assert.That(map.IsSolid(new Vector2(20f, 10f)), Is.True);
        map.RemoveHorizontalBarrier(10f);
        Assert.That(map.IsSolid(new Vector2(20f, 10f)), Is.False);
    }
}
