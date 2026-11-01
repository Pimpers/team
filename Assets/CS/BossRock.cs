using UnityEngine;

// A rock shaken loose from the ceiling when the bear pounds the floor (BossBrain). A red mark flickers on
// the floor where it will land, then the rock drops; it hurts the player if it falls on them (on the floor
// or on a ledge on the way down) and crumbles where it lands. Dodge: step out of the marked spot.
public class BossRock : MonoBehaviour
{
    const float WarnTime = 0.8f, FallSpeed = 16f, DropHeight = 9f, HitHalfWidth = 0.7f, Damage = 0.5f, Scale = 1.5f;

    float floorY, warn;
    bool falling;
    Enemy owner;
    SpriteRenderer rock, mark;

    public static void Spawn(float x, float floorY, float delay, Sprite sprite, Enemy owner)
    {
        if (sprite == null) return;
        var go = new GameObject("BossRock");
        go.transform.position = new Vector3(x, floorY + DropHeight, 0f);
        go.transform.localScale = Vector3.one * Scale;
        var r = go.AddComponent<BossRock>();
        r.floorY = floorY;
        r.warn = WarnTime + delay;
        r.owner = owner;
        r.rock = go.AddComponent<SpriteRenderer>();
        r.rock.sprite = sprite;
        r.rock.sortingOrder = 6;
        r.rock.enabled = false;
        r.mark = SkillFx.Bar(new Vector3(x - 0.8f, floorY + 0.06f, 0f), new Vector3(x + 0.8f, floorY + 0.06f, 0f), 0.14f, new Color(1f, 0.25f, 0.2f, 0f), 5);
    }

    void Update()
    {
        if (!falling)
        {
            warn -= Time.deltaTime;
            var c = mark.color;
            c.a = 0.55f + 0.4f * Mathf.Sin(Time.time * 25f); // flickers: "something is coming"
            mark.color = c;
            if (warn > 0f) return;
            falling = true;
            rock.enabled = true;
        }

        var p = transform.position;
        p.y -= FallSpeed * Time.deltaTime;
        transform.position = p;
        rock.transform.Rotate(0f, 0f, 360f * Time.deltaTime);

        float bottom = rock.bounds.min.y;
        if (HitsPlayer(p.x, bottom, rock.bounds.max.y)) { Crumble(); return; }
        if (bottom <= floorY) Crumble();
    }

    bool HitsPlayer(float x, float bottom, float top)
    {
        if (owner == null || owner.IsDead || PlayerStats.Instance == null) return false;
        Vector2 to = owner.ToPlayerFeet();
        float feetX = owner.transform.position.x + to.x, feetY = owner.transform.position.y + to.y;
        if (Mathf.Abs(feetX - x) > HitHalfWidth) return false;
        if (bottom > feetY + 1.3f || top < feetY) return false; // not yet down to the cat's head / already past its feet
        owner.HitPlayer(Damage, 1f, Mathf.Sign(feetX - x));
        return true;
    }

    void OnDestroy() { if (mark != null) Destroy(mark.gameObject); }

    void Crumble()
    {
        var at = new Vector3(transform.position.x, Mathf.Max(floorY, rock.bounds.min.y), 0f);
        Vfx.Play("debris", at + Vector3.up * 0.3f, 1f);
        Vfx.Play("dust_small", at, 1f);
        Sfx.Play("land_heavy", 0.5f, 1.3f);
        Destroy(gameObject);
    }
}
