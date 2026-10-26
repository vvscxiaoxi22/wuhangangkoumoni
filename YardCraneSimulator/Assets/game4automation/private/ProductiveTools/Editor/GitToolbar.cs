#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEditor.SceneManagement;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;

namespace game4automation
{
    public class Game4AutomationGitToolbar : EditorWindow
    {

        [MenuItem("game4automation/GIT/FETCH (get changes from with GITHUB - no changes in project)", false, 54)]
        private static void GitFetch()
        {
            var path = Application.dataPath;
            var parent = System.IO.Directory.GetParent(path);
            var upparent = System.IO.Directory.GetParent(parent.ToString());
         
            var arg = "/command:fetch";
     
            
            StartBatWithArgument("\"C:\\Program Files\\TortoiseGit\\bin\\TortoiseGitProc.exe\"",arg);
        }
        
        [MenuItem("game4automation/GIT/PULL (Tortoise)", false, 54)]
        private static void GitSync()
        {
            var path = Application.dataPath;
            var parent = System.IO.Directory.GetParent(path);
            var upparent = System.IO.Directory.GetParent(parent.ToString());
         
            var arg = "/command:pull";
     
            
            StartBatWithArgument("\"C:\\Program Files\\TortoiseGit\\bin\\TortoiseGitProc.exe\"",arg);
        }
        
        [MenuItem("game4automation/GIT/COMMIT (Tortoise)", false, 55)]
        private static void StartGitCommit()
        {
            CommitToGit();
        }

        [MenuItem("game4automation/GIT/LOG (Tortoise)", false, 56)]
        private static void StartGitLog()
        {
            GitLog();
        }

        private static void GitLog()
        {
            var path = Application.dataPath;
            var parent = System.IO.Directory.GetParent(path);
            var upparent = System.IO.Directory.GetParent(parent.ToString());
         
            var arg = "/command:log";
     
            
            StartBatWithArgument("\"C:\\Program Files\\TortoiseGit\\bin\\TortoiseGitProc.exe\"",arg);
        }

        private static void CommitToGit()
        {

            var path = Application.dataPath;
            var parent = System.IO.Directory.GetParent(path);
            var upparent = System.IO.Directory.GetParent(parent.ToString());
            var build = System.Int32.Parse(Global.Build);
            var futurebuild = (build + 1).ToString();
            var arg = "/command:commit /path \""+ path +"\" /closeonend:2";
            StartBatWithArgument("\"C:\\Program Files\\TortoiseGit\\bin\\TortoiseGitProc.exe\"",arg);

        }
        
        
        private static void StartBatWithArgument(string bat, string argument)
        {
            
            var psi = new ProcessStartInfo(bat) 
            
            {
               
            };
            try
            {
                psi.Arguments = argument;
                var process = Process.Start(psi);
       
                while (!process.HasExited)
                {
                    //update UI
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
            }
        }


    }
}
#endif