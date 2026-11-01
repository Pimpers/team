using UnityEngine;

// A rock the bear hurls at the cat (BossBrain's throw): it flies in an arc to where the cat was standing, a red mark
// flickering on the floor there meanwhile. It hurts the cat if it hits on the way, or if the cat is standing next
// to where it smashes; then it shatters. Dodge: step out from the mark, or jump over it as it lands.
public class BossThrownRock : MonoBehaviour
{
    const float Gravity = 22f, HitRadius = 0.7f, SplashHalfWidth = 1.1f, Damage = 0.5f, Scale = 1.5f;

    Vector2 velocity;
    float groundY;
    Enemy owner;
    SpriteRenderer rock, mark;

    // Lands at (targetX, targetY) after `flightTime` seconds.
    public static void Spawn(Vector3 from, float targetX, float targetY, float flightTime, Sprite sprite, Enemy owner)
    {
        if (sprite == null) return;
        var map = StageMap.Instance;
        if (map != null) targetX = Mathf.Clamp(targetX, 1f, map.mapSize.x - 1f);
        var go = new GameObject("BossThrownRock");
        go.transform.position = from;
        go.transform.localScale = Vector3.one * Scale;
        var r = go.AddComponent<BossThrownRock>();
        r.owner = owner;
        r.groundY = targetY;
        float t = Mathf.Max(0.3f, flightTime);
        r.velocity = new Vector2((targetX - from.x) / t, (targetY - from.y + 0.5f * Gravity * t * t) / t);
        r.rock = go.AddComponent<SpriteRenderer>();
        r.rock.sprite = sprite;
        r.rock.sortingOrder = 6;
        r.mark = SkillFx.Bar(new Vector3(targetX - 0.9f, targetY + 0.06f, 0f), new Vector3(targetX + 0.9f, targetY + 0.06f, 0f), 0.14f, new Color(1f, 0.25f, 0.2f, 0.8f), 5);
    }

    void Update()
    {
        if (mark != null) { var c = mark.color; c.a = 0.55f + 0.4f * Mathf.Sin(Time.time * 25f); mark.color = c; }
        velocity.y -= Gravity * Time.deltaTime;
        var p = transform.position + (Vector3)(velocity * Time.deltaTime);
        transform.position = p;
        transform.Rotate(0f, 0f, -Mathf.Sign(velocity.x) * 540f * Time.deltaTime);

        if (PlayerCentre(out Vector2 cat) && Vector2.Distance(p, cat) < HitRadius)
        {
            owner.HitPlayer(Damage, 1.5f, Mathf.Sign(velocity.x));
            Shatter(false);
            return;
        }
        var map = StageMap.Instance;
        bool wall = map != null && map.IsSolid(p);
        if ((velocity.y < 0f && p.y <= groundY + 0.2f) || wall) Shatter(true);
    }

    bool PlayerCentre(out Vector2 centre)
    {
        centre = Vector2.zero;
        if (owner == null || owner.IsDead || PlayerStats.Instance == null) return false;
        Vector2 feet = (Vector2)owner.transform.position + owner.ToPlayerFeet();
        centre = feet + Vector2.up * 0.6f;
        return true;
    }

    // Smashes: chips fly, and a cat standing right beside it is caught in the splash.
    void Shatter(bool splash)
    {
        var at = new Vector3(transform.position.x, Mathf.Max(groundY, transform.position.y - 0.3f), 0f);
        Vfx.Play("debris", at + Vector3.up * 0.3f, 1.2f);
        Vfx.Play("dust_mid", at, 1.2f);
        Sfx.Play("land_heavy", 0.7f, 1.2f);
        CameraFollow.Shake(0.12f, 0.1f);
        if (splash && PlayerStats.Instance != null && owner != null && !owner.IsDead)
        {
            Vector2 feet = (Vector2)owner.transform.position + owner.ToPlayerFeet();
            var body = PlayerStats.Instance.Body;
            if (Mathf.Abs(feet.x - at.x) < SplashHalfWidth && Mathf.Abs(feet.y - at.y) < 0.6f && (body == null || !body.IsAirborne))
                owner.HitPlayer(Damage, 1.2f, Mathf.Sign(feet.x - at.x));
        }
        Destroy(gameObject);
    }

    void OnDestroy() { if (mark != null) Destroy(mark.gameObject); }
}
