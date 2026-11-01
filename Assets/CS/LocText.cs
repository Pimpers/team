using TMPro;
using UnityEngine;

// Shows a text-table line (Loc, Resources/Strings/ko.txt) in the TMP text on this object. SceneTextLinker
// (editor) adds it to the texts the scene builders write, and the scene keeps the line it was built with, so it
// still reads right in the editor. It runs before other scripts and only while the label still shows that
// built-in line: a script that writes its own text there (the cat's name, the gold) is never overwritten.
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(TMP_Text))]
public class LocText : MonoBehaviour
{
    public string key;
    [Tooltip("The text the scene was saved with; it is swapped for the table line only while the label still shows it.")]
    public string source;

    void Awake()
    {
        var t = GetComponent<TMP_Text>();
        if (t != null && !string.IsNullOrEmpty(key) && t.text == source) t.text = Loc.T(key);
    }
}
