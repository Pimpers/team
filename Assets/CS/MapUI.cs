using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A small Skul-style chapter map: 3 nodes in a row (Stage 1, Stage 2, Boss) connected
// by lines. Shown before the run starts and again between stages so the player can see
// where they are before pressing Continue to step into the next room.
public class MapUI : MonoBehaviour
{
    public GameObject panel;
    public TMP_Text titleText;
    public Image[] nodeIcons;      // size 3: Stage1, Stage2, Boss
    public TMP_Text[] nodeLabels;  // size 3
    public Button continueButton;
    public TMP_Text continueButtonLabel;

    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f);
    public Color currentColor = new Color(1f, 0.82f, 0.2f);
    public Color clearedColor = new Color(0.35f, 0.8f, 0.4f);

    Action onContinue;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
    }

    // stageAboutToStart: 1, 2 or 3. Nodes before it show cleared, that one shows
    // current (about to enter), the rest show locked.
    public void Show(int stageAboutToStart, Action onContinueCallback)
    {
        onContinue = onContinueCallback;
        if (panel != null) panel.SetActive(true);
        if (titleText != null) titleText.text = "CHAPTER 1";

        for (int i = 0; i < nodeIcons.Length; i++)
        {
            int stageNumber = i + 1;
            Color c = stageNumber < stageAboutToStart ? clearedColor
                    : stageNumber == stageAboutToStart ? currentColor
                    : lockedColor;
            if (nodeIcons[i] != null) nodeIcons[i].color = c;
        }

        if (continueButtonLabel != null)
            continueButtonLabel.text = stageAboutToStart == 1 ? "Enter" : "Continue";
    }

    void OnContinueClicked()
    {
        if (panel != null) panel.SetActive(false);
        onContinue?.Invoke();
    }
}
