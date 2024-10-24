using GameLib.Alg;
using UnityEngine;
using UnityEngine.UI;

public class CommandSuggestionContainer : MonoBehaviour
{
    public GameObject СommandSuggestionPrefab;
    public void Clear()
    {
        transform.DestroyChildren();
    }

    public void AddItem((string Suggestion, int Score) suggestion)
    {
        var item = Instantiate(СommandSuggestionPrefab, transform, false);
        item.GetComponent<Text>().text = $"{suggestion.Score}{suggestion.Suggestion}";
    }
}
