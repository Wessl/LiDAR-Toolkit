using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    
    [InitializeOnLoad]
    public class Startup
    {
        private static string startupMessagePlayerPrefKey = "HasShownStartPopup";
        static Startup()
        {
            Debug.Log("Up and running");
            if (PlayerPrefs.GetString(startupMessagePlayerPrefKey, "") == "")
            {
                EditorMessagePopup editorMessagePopup = ScriptableObject.CreateInstance<EditorMessagePopup>();
                editorMessagePopup.ShowStartupMessage();
                PlayerPrefs.SetString(startupMessagePlayerPrefKey, "Shown");
            }

            Debug.Log("boi, remember to check the editor message popup string playerpref thing...");
        }
    }
    public class EditorMessagePopup : UnityEditor.Editor
    {
        public void ShowStartupMessage()
        {
            EditorUtility.DisplayDialog("Welcome to the LiDAR Gameplay Toolkit!",
                "TL;DR: Put the DrawPoints and LiDAR scripts on a camera to get started!\r\n" + 
                "Check the documentation in {todo} for more details on how to use the asset.\r\n" +
                "There is a custom scene set up in Scenes/LiDARSetup that contains an example setup for the LiDAR.", "Ok");    
        }
        
        
        
    }    
}

