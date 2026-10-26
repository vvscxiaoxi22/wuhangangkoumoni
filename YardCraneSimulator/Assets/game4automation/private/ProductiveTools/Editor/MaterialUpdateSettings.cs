using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace game4automation
{
    [Serializable]
    public class MaterialUpdateMapping
    {
        public Material SourceMaterial;
        public Color SourceColor;
        public string SourceName;
        public Material AssignMaterial;
        public Material BlueprintMaterial;
        public string NewMaterialName;
    }

    [CreateAssetMenu(fileName = "MaterialUpdate", menuName = "game4automation/Add material update settings", order = 1)]
    public class MaterialUpdateSettings : ScriptableObject
    {
        public string NewMaterialFolder;

        [SerializeField] public List<MaterialUpdateMapping> Mappings;

        private Material SelectMaterial(Material currentmat)
        {
            int findnr = -1;
            int i = 0;
            Material newmat = null;

            foreach (var mapping in Mappings)
            {
                if (mapping.SourceMaterial == currentmat)
                {
                    findnr = i;
                    break;
                }
                else
                {
                    if (mapping.SourceColor == currentmat.color)
                    {
                        findnr = i;
                        break;
                    }
                    else
                    {
                        if (mapping.SourceName != "")
                        {
                            Match m = Regex.Match(currentmat.name, mapping.SourceName, RegexOptions.None);
                            if (m.Success)
                            {
                                findnr = i;
                                break;
                            }
                        }
                    }
                }

                i++;
            }

            if (findnr > -1)
            {
                if (Mappings[i].AssignMaterial != null)
                {
                    newmat = Mappings[i].AssignMaterial;
                }
                else
                {
                    if (Mappings[i].BlueprintMaterial != null)
                    {
                        // is material like blueprint already existing
                        var name = Mappings[i].NewMaterialName;
                        string[] folders = {NewMaterialFolder};
                        string[] assets = AssetDatabase.FindAssets(name + " t:material", folders);

                        if (assets.Length != 0)
                        {
                            newmat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(assets[0]));
                        }
                        else
                        {
                            // no - create new material based on blueprint
                            var path = Path.Combine(NewMaterialFolder, name + ".mat");
                            //var uniquefilename = AssetDatabase.GenerateUniqueAssetPath(path);
                            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(Mappings[i].BlueprintMaterial), path);
                            //AssetDatabase.CreateAsset(Mappings[i].BlueprintMaterial, uniquefilename);
                            newmat = AssetDatabase.LoadAssetAtPath<Material>(path);
                            newmat.color = Mappings[i].SourceColor;
                        }
                    }
                }
            }

            return newmat;
        }

        public void UpdateMaterials(GameObject go)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material[] sharedMaterialsCopy = renderer.sharedMaterials;

                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    Material newmaterial = SelectMaterial(sharedMaterialsCopy[i]);
                    if (newmaterial != null)
                        sharedMaterialsCopy[i] = newmaterial;
                }

                renderer.sharedMaterials = sharedMaterialsCopy;
            }
        }
    }
}