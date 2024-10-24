using UnityEngine;
using NaughtyAttributes;

public class Assemblies : MonoBehaviour
{
    public enum FilteringMode
    {
        WhiteList, // Only include assemblies that start with any of the prefixes in the list
        BlackList  // Exclude assemblies that start with any of the prefixes in the list
    }

    [InfoBox("Filtering Mode Options:\n" +
             "1. WhiteList: Interpret the provided list as a whitelist. Only analyze assemblies whose names start with any of the prefixes in this list. \n" +
             "2. BlackList: Interpret the provided list as a blacklist. Ignore assemblies whose names start with any of the prefixes in this list.\n\n" +
             "Note: You can inspect the generated assemblies and their names to determine which prefixes to use. " +
             "This allows for more granular control over which assemblies are included or excluded for UConsole to check exported methods and variables.")]
    public FilteringMode AssemblyFilteringMode;

    public string[] List;
}