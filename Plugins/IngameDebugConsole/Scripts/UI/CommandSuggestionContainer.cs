using System.Collections.Generic;
using GameLib.Alg;
using uconsole;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Image = UnityEngine.UI.Image;

public class CommandSuggestionContainer : MonoBehaviour
{
    public GameObject СommandSuggestionPrefab;
    public Image Background;
    private int _index;
    private List<string> _suggestions = new List<string>(10);
    
    public void Clear()
    {
        transform.DestroyChildren();
        Background.enabled = false;
        _index = 0;
        _suggestions.Clear();
    }

    public void AddItem(ConsoleSystem.ConsoleMethodInfo methodInfo )
    {
        Background.enabled = true;
        var key = (_index + 1) % 10;
        var item = Instantiate(СommandSuggestionPrefab, transform, false);
        item.GetComponent<Text>().text = $"[{key}] {methodInfo.FullName} or {methodInfo.AliasName}";
        item.GetComponent<Text>().color = Color.white;
        _suggestions.Add(methodInfo.FullName+"()");
        _index++;
    }
    
    public void AddItem(ConsoleSystem.ConsoleVariableInfo varInfo)
    {
        Background.enabled = true;
        var key = (_index + 1) % 10;
        var item = Instantiate(СommandSuggestionPrefab, transform, false);
        item.GetComponent<Text>().text = $"[{key}] {varInfo.FullName} or {varInfo.AliasName}";
        item.GetComponent<Text>().color = Color.yellow;
        _suggestions.Add(varInfo.FullName);
        _index++;
    }

    public string GetSuggestion(int index)
    {
        return _suggestions[index];
    }
}
