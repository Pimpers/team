using UnityEngine;
using TMPro;

// A mercenary wandering around the lobby floor. Walks to random spots (Run frames),
// pauses (Idle frames), and shows a name tag above its head. When picked it stops,
// faces right and plays its attack once; while another cat is picked it keeps wandering.
// Frames are trimmed to different sizes with a bottom-left pivot, so the child `visual`
// is re-offset every frame to keep the feet pinned to this object's position.
public class LobbyCharacterDisplay : MonoBehaviour
{
    public CharacterDefinition character;
    public int characterIndex;
    public Transform visual;
    [Tooltip("Parent of the job plate shown above the head (hidden while zoomed in).")]
    public GameObject tagRoot;
    public SpriteRenderer jobPlate;
    public TMP_Text jobText;

    [Header("Wandering")]
    public float minX = -6.5f;
    public float maxX = 6.5f;
    public float walkSpeed = 1.3f;
    public Vector2 pauseRange = new Vector2(1f, 3f);

    SpriteRenderer sr;
    Sprite[] playing;
    float playingFps;
    bool playingOnce;
    int frame;
    float timer;

    bool selected;
    bool walking;
    float targetX;
    float pauseTimer;

    void Awake()
    {
        sr = visual.GetComponent<SpriteRenderer>();
        pauseTimer = Random.Range(0.2f, pauseRange.y);
        Play(character.idleFrames, character.framesPerSecond, false);
        RefreshNameTag();
    }

    // Plate above the head with the cat's tag and job (하양이 · 검사 / 치즈 · 궁수). The player-given name isn't shown here - it's given when
    // the cat is picked. The plate is a 9-sliced sprite sized to fit the text.
    public void RefreshNameTag()
    {
        if (jobText == null) return;
        // Tag plus job (하양이 · 검사), so the player knows which cat is which before clicking one.
        string label = string.IsNullOrEmpty(character.tagName) ? character.jobName
            : string.IsNullOrEmpty(character.jobName) ? character.tagName : character.tagName + " · " + character.jobName;
        jobText.text = label;
        if (jobPlate != null)
        {
            var plate = WorldPlate();
            if (plate != null) jobPlate.sprite = plate;
            Vector2 textSize = jobText.GetPreferredValues(label);
            jobPlate.size = new Vector2(textSize.x + 0.6f, Mathf.Max(0.6f, textSize.y + 0.2f));
        }
    }

    // The UI skin's wood button as a world-space 9-sliced plate (same look as the UI's buttons). The skin
    // sprite is 200 px/unit for the canvas; here it is re-cut at 32 px/unit so its rim is as thick as the
    // old plate's.
    static Sprite worldPlate;

    static Sprite WorldPlate()
    {
        if (worldPlate != null) return worldPlate;
        var src = UISkin.Instance != null ? UISkin.Instance.buttonWood : null;
        if (src == null || src.texture == null) return null;
        worldPlate = Sprite.Create(src.texture, src.rect, new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, src.border);
        worldPlate.name = "LobbyPlate";
        return worldPlate;
    }

    public void ShowNameTag(bool show)
    {
        if (tagRoot != null) tagRoot.SetActive(show);
    }

    public void SetSelected(bool on)
    {
        if (on && !selected)
        {
            walking = false;
            sr.flipX = false;
            if (character.attackFrames != null && character.attackFrames.Length > 0)
                Play(character.attackFrames, character.attackFramesPerSecond, true);
            else
                Play(character.idleFrames, character.framesPerSecond, false);
        }
        else if (!on && selected)
        {
            pauseTimer = Random.Range(pauseRange.x, pauseRange.y);
        }
        selected = on;
    }

    // World-space hit test for clicking the character itself.
    public bool Contains(Vector2 worldPoint)
    {
        Bounds b = sr.bounds;
        return worldPoint.x >= b.min.x - 0.2f && worldPoint.x <= b.max.x + 0.2f && worldPoint.y >= b.min.y && worldPoint.y <= b.max.y + 0.2f;
    }

    void Update()
    {
        if (!selected) Wander();
        Animate();
    }

    void Wander()
    {
        if (walking)
        {
            float step = walkSpeed * Time.deltaTime;
            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, targetX, step);
            transform.position = p;
            if (Mathf.Abs(p.x - targetX) < 0.01f)
            {
                walking = false;
                pauseTimer = Random.Range(pauseRange.x, pauseRange.y);
                Play(character.idleFrames, character.framesPerSecond, false);
            }
            return;
        }

        if (playingOnce) return; // let a selection attack finish before walking off
        pauseTimer -= Time.deltaTime;
        if (pauseTimer > 0f) return;

        // Pick a new spot at least 1.5 units away.
        float x = transform.position.x;
        for (int i = 0; i < 5; i++)
        {
            targetX = Random.Range(minX, maxX);
            if (Mathf.Abs(targetX - x) > 1.5f) break;
        }
        walking = true;
        sr.flipX = targetX < x;
        Play(character.runFrames, character.framesPerSecond * 1.25f, false);
    }

    void Play(Sprite[] frames, float fps, bool once)
    {
        playing = frames;
        playingFps = fps;
        playingOnce = once;
        frame = 0;
        timer = 0f;
        if (frames != null && frames.Length > 0) ShowFrame(frames[0]);
    }

    void Animate()
    {
        if (playing == null || playing.Length == 0) return;
        timer += Time.deltaTime;
        if (timer >= 1f / playingFps)
        {
            timer = 0f;
            frame++;
            if (frame >= playing.Length)
            {
                if (playingOnce) { Play(character.idleFrames, character.framesPerSecond, false); return; }
                frame = 0;
            }
        }
        ShowFrame(playing[frame]);
    }

    void ShowFrame(Sprite f)
    {
        sr.sprite = f;
        float s = visual.localScale.x;
        if (character.TryGetVisualOffset(f, sr.flipX, s, out Vector2 aligned))
        {
            // Aligned offsets center the body on the parent; this object sits at the feet,
            // so lift by half the reference body height.
            visual.localPosition = new Vector3(aligned.x, aligned.y + character.alignReferenceSize.y * 0.5f * s, 0f);
            return;
        }
        Bounds b = f.bounds;
        float cx = b.center.x * s;
        visual.localPosition = new Vector3(sr.flipX ? cx : -cx, -b.min.y * s, 0f);
    }
}
