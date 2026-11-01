using UnityEngine;

// On the Player in each Stage scene. Before anything else runs, swaps the Player's
// animation frames and attack type to the mercenary chosen in the lobby.
[DefaultExecutionOrder(-100)]
public class PlayerCharacterLoader : MonoBehaviour
{
    [Tooltip("Same order as the lobby: 0 = Meow Knight, 1 = Meowolas.")]
    public CharacterDefinition[] characters;

    public CharacterDefinition Current { get; private set; }

    void Awake()
    {
        if (characters == null || characters.Length == 0) return;
        int index = Mathf.Clamp(CharacterSelection.SelectedIndex, 0, characters.Length - 1);
        Current = characters[index];
        if (Current == null) return;

        var pc = GetComponent<PlayerController2D>();
        if (pc != null)
        {
            pc.character = Current;
            pc.idleFrames = Current.idleFrames;
            pc.runFrames = Current.runFrames;
            pc.attackFrames = Current.attackFrames;
            pc.damageFrames = Current.damageFrames;
            pc.jumpFrames = Current.jumpFrames;
            pc.deathFrames = Current.deathFrames;
            pc.dodgeFrames = Current.dodgeFrames;
            pc.framesPerSecond = Current.framesPerSecond;
            pc.attackFramesPerSecond = Current.attackFramesPerSecond;
            pc.damageFramesPerSecond = Current.damageFramesPerSecond;
            pc.jumpFramesPerSecond = Current.jumpFramesPerSecond;
            pc.deathFramesPerSecond = Current.deathFramesPerSecond;
        }

        var attack = GetComponent<PlayerAttack>();
        if (attack != null)
        {
            attack.ranged = Current.ranged;
            attack.projectileSprite = Current.projectileSprite;
            attack.projectileSpeed = Current.projectileSpeed;
            attack.projectileRange = Current.projectileRange;
            attack.releaseDelay = Current.releaseDelay;
        }

        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && Current.idleFrames != null && Current.idleFrames.Length > 0)
            sr.sprite = Current.idleFrames[0];
    }
}
