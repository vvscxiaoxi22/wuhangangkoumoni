// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

using UnityEditor;
using UnityEngine;

namespace game4automation
{
    //! Parts4cad Postprocessor when importing Collada file from Parts4Cad
    public class ColladaImportPostprocess : AssetPostprocessor
    {

        public void OnPreprocessModel()
        {

            Parts4CadSettings settings =
                (Parts4CadSettings) AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/game4automation/Parts4Cad/Parts4CadSettings.asset");

            if (settings != null)
            {
                ModelImporter modelImporter = assetImporter as ModelImporter;
                if (assetPath.Contains("parts4cad"))
                {
                    modelImporter.globalScale = settings.ImportScale;
                    modelImporter.addCollider = false;
                    modelImporter.preserveHierarchy = true;
                    Debug.Log("parts4cad Import Posprocessor");
                }
            }
        }
    }
}