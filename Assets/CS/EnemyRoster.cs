using UnityEngine;

// The regular monsters, for StageRandomizer to mix a stage's line-up each run (Resources/EnemyRoster.asset,
// written by the editor's EnemyAssets). The stages were drawn mostly with white rats (6 of every 10 monsters), and
// each stage had only three kinds; the randomizer turns some of the rats into the other kinds.
public class EnemyRoster : ScriptableObject
{
    public EnemyDefinition rat;
    public EnemyDefinition[] others; // spider, slime, mushroom

    static EnemyRoster cached;
    public static EnemyRoster Load() => cached != null ? cached : (cached = Resources.Load<EnemyRoster>("EnemyRoster"));
}
