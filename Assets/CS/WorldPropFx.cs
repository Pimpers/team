using System.Collections;
using UnityEngine;

// Small SunnyLand item flash used by both reward chests.
public class WorldPropFx : MonoBehaviour
{
    static Sprite[] itemFrames;
    SpriteRenderer rendererPart;
    float frameTime;

    public static void PlayItemFeedback(Vector3 position, float scale = 1f)
    {
        if (itemFrames == null)
        {
            itemFrames = new Sprite[4];
            for (int i = 0; i < itemFrames.Length; i++)
                itemFrames[i] = Resources.Load<Sprite>($"WorldProps/SunnyLand/ItemFeedback/item-feedback-{i + 1}");
        }
        if (itemFrames[0] == null) return;

        var go = new GameObject("ItemFeedback");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        var fx = go.AddComponent<WorldPropFx>();
        fx.rendererPart = go.AddComponent<SpriteRenderer>();
        fx.rendererPart.sprite = itemFrames[0];
        fx.rendererPart.sortingOrder = 30;
        fx.StartCoroutine(fx.Play());
    }

    IEnumerator Play()
    {
        for (int i = 0; i < itemFrames.Length; i++)
        {
            rendererPart.sprite = itemFrames[i];
            frameTime = 0f;
            while (frameTime < 0.075f)
            {
                frameTime += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        Destroy(gameObject);
    }
}
