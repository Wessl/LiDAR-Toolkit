using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

// https://docs.unity3d.com/Manual/CommandLineArguments.html
public class JenkinsBuild
{

    static string[] EnabledScenes = FindEnabledEditorScenes();
    static BuildOptions buildOption;

    // called from Jenkins
    public static void BuildMacOS()
    {
        var args = FindArgs();

        string fullPathAndName = args.targetDir + args.appName + ".app";
        Enum.TryParse<BuildOptions>(args.buildOptions, out buildOption);
        BuildProject(EnabledScenes, fullPathAndName, BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX, buildOption);
    }

    // called from Jenkins
    public static void BuildWindows64()
    {
        var args = FindArgs();

        string fullPathAndName = args.targetDir + args.appName + ".exe";
        Enum.TryParse<BuildOptions>(args.buildOptions, out buildOption);
        BuildProject(EnabledScenes, fullPathAndName, BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, buildOption);
    }

    // called from Jenkins
    public static void BuildLinux()
    {
        var args = FindArgs();

        string fullPathAndName = args.targetDir + args.appName;
        Enum.TryParse<BuildOptions>(args.buildOptions, out buildOption);
        BuildProject(EnabledScenes, fullPathAndName, BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64, buildOption);
    }

    private static Args FindArgs()
    {
        var returnValue = new Args();

        // find: -executeMethod
        //   +1: JenkinsBuild.BuildMacOS
        //   +2: FindTheGnome
        //   +3: D:\Jenkins\Builds\Find the Gnome\47\output
        //   +4: Development (BuildOptions) https://docs.unity3d.com/2020.3/Documentation/ScriptReference/BuildOptions.html
        string[] args = System.Environment.GetCommandLineArgs();
        var execMethodArgPos = -1;
        bool allNecessaryArgsFound = false;
        bool allOptionalArgsFound = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-executeMethod")
            {
                execMethodArgPos = i;
            }
            var realPos = execMethodArgPos == -1 ? -1 : i - execMethodArgPos - 2;
            if (realPos < 0)
                continue;

            if (realPos == 0)
                returnValue.appName = args[i];
            if (realPos == 1)
            {
                returnValue.targetDir = args[i];
                if (!returnValue.targetDir.EndsWith(System.IO.Path.DirectorySeparatorChar + ""))
                    returnValue.targetDir += System.IO.Path.DirectorySeparatorChar;
                allNecessaryArgsFound = true;
            }

            if (realPos == 2)
            {
                returnValue.buildOptions = args[i];
                allOptionalArgsFound = true;

            }
        }

        if (!allNecessaryArgsFound)
            System.Console.WriteLine("[JenkinsBuild] Incorrect Parameters for -executeMethod Format: -executeMethod JenkinsBuild.BuildWindows64 <app name> <output dir>");
        if (!allOptionalArgsFound)
            System.Console.WriteLine("[JenkinsBuild] BuildOptions parameter not supplied as argument, defaulting to 'None'. Complete format: Format: -executeMethod JenkinsBuild.BuildWindows64 <app name> <output dir> <BuildOptions>");

        return returnValue;
    }


    private static string[] FindEnabledEditorScenes()
    {

        List<string> EditorScenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled)
                EditorScenes.Add(scene.path);

        return EditorScenes.ToArray();
    }

    // e.g. BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX
    private static void BuildProject(string[] scenes, string targetDir, BuildTargetGroup buildTargetGroup, BuildTarget buildTarget, BuildOptions buildOptions)
    {
        System.Console.WriteLine("[JenkinsBuild] Building:" + targetDir + " buildTargetGroup:" + buildTargetGroup.ToString() + " buildTarget:" + buildTarget.ToString());

        // https://docs.unity3d.com/ScriptReference/EditorUserBuildSettings.SwitchActiveBuildTarget.html
        bool switchResult = EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
        if (switchResult)
        {
            System.Console.WriteLine("[JenkinsBuild] Successfully changed Build Target to: " + buildTarget.ToString());
        }
        else
        {
            System.Console.WriteLine("[JenkinsBuild] Unable to change Build Target to: " + buildTarget.ToString() + " Exiting...");
            return;
        }

        // https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html
        BuildReport buildReport = BuildPipeline.BuildPlayer(scenes, targetDir, buildTarget, buildOptions);
        BuildSummary buildSummary = buildReport.summary;
        if (buildSummary.result == BuildResult.Succeeded)
        {
            System.Console.WriteLine("[JenkinsBuild] Build Success: Time:" + buildSummary.totalTime + " Size:" + buildSummary.totalSize + " bytes");
        }
        else
        {
            System.Console.WriteLine("[JenkinsBuild] Build Failed: Time:" + buildSummary.totalTime + " Total Errors:" + buildSummary.totalErrors);
        }
    }

    private class Args
    {
        public string appName = "AppName";
        public string targetDir = "~/Desktop";
        public string buildOptions = "None";
    }
}