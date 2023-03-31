using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace Editor
{
    
    [InitializeOnLoad]
    public class Startup
    {
        private static readonly string startupMessagePlayerPrefKey = "HasShownStartPopup";
        private static readonly string hdrpWarningMessage = "HDRPWarningMessage";
        public static string HdrpWarningMessage => hdrpWarningMessage;
        static Startup()
        {
            if (PlayerPrefs.GetString(startupMessagePlayerPrefKey, "") == "")
            {
                EditorMessagePopup editorMessagePopup = ScriptableObject.CreateInstance<EditorMessagePopup>();
                editorMessagePopup.ShowStartupMessage();
                PlayerPrefs.SetString(startupMessagePlayerPrefKey, "Shown");
            }

            if (GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset && PlayerPrefs.GetString(startupMessagePlayerPrefKey, "") == "")
            {
                EditorMessagePopup editorMessagePopup = ScriptableObject.CreateInstance<EditorMessagePopup>();
                editorMessagePopup.ShowHDRPNotCompatibleMessage();
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

        public void ShowHDRPNotCompatibleMessage()
        {
            var dontShowAgain = EditorUtility.DisplayDialog("HDRP rendering asset detected",
                "You seem to be using a HDRP rendering asset.\r\n" + 
                "This is not supported, and points are likely to not render correctly.\r\n" +
                "Please use the built-in rendering pipeline (BRP) or the Universal Render Pipeline (URP) instead.", "Don't show again", "Cancel"); 
            if (dontShowAgain) PlayerPrefs.SetString(Startup.HdrpWarningMessage, "Shown");
        }
        
        
        
    }    
}

