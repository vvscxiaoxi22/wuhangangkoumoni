using HikvisionFourDisplay.Runtime.Security;
using UnityEditor;
using UnityEngine;

namespace HikvisionFourDisplay.Editor
{
    /// <summary>
    /// Local operator tool for writing/removing feed credentials in the Windows
    /// Credential Manager. Nothing entered here is written to any project file or log.
    /// </summary>
    public sealed class FeedCredentialManagerWindow : EditorWindow
    {
        private string credentialKey = "feed1";
        private string username = string.Empty;
        private string password = string.Empty;

        [MenuItem("Tools/Hikvision Four Display/Manage Feed Credentials")]
        private static void Open()
        {
            GetWindow<FeedCredentialManagerWindow>("HFD Credentials");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Stored as a CRED_TYPE_GENERIC entry in the Windows Credential Manager, " +
                "DPAPI-protected under this Windows user profile. Never written to project " +
                "files, source control, or logs.", MessageType.Info);

            EditorGUILayout.Space();
            credentialKey = EditorGUILayout.TextField("Credential Key", credentialKey);
            username = EditorGUILayout.TextField("Username", username);
            password = EditorGUILayout.PasswordField("Password", password);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                {
                    if (WindowsCredentialManagerProvider.Save(credentialKey, username, password))
                    {
                        EditorUtility.DisplayDialog("HFD Credentials", $"Saved credential '{credentialKey}'.", "OK");
                        password = string.Empty;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("HFD Credentials", "Save failed. See Console for the Win32 error.", "OK");
                    }
                }

                if (GUILayout.Button("Check Exists"))
                {
                    var exists = new WindowsCredentialManagerProvider().TryGet(credentialKey, out _);
                    EditorUtility.DisplayDialog("HFD Credentials", exists ? "Credential exists." : "No credential stored for this key.", "OK");
                }

                if (GUILayout.Button("Delete"))
                {
                    if (EditorUtility.DisplayDialog("HFD Credentials", $"Delete credential '{credentialKey}'?", "Delete", "Cancel"))
                        WindowsCredentialManagerProvider.Delete(credentialKey);
                }
            }
        }
    }
}
