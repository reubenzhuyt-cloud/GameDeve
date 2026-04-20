#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class Match3SimulatorLogMenu
{
    [MenuItem("Tools/Match3/Run Demo (Console Log)")]
    public static void RunDemo()
    {
        Match3BoardSimulator.RunDemoToConsole();
    }
}
#endif
