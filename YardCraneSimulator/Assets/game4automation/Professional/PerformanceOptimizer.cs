using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using UnityEditor;

namespace game4automation
{
    public class PerformanceOptimizer : Game4AutomationBehavior
    {
        public bool CreateSubObjectForMesh = true;
        public bool CreateStaticMesh = true;
        public bool IncludeNonStaticElements = false;
        public bool ExcludeGroups = true;
        public bool DeleteCombinedMeshes = false;
        public List<String> IncludedGroupsForMeshOptimizer;


        private const int Mesh16BitBufferVertexLimit = 65535;

        [HideInInspector] public bool deactivateCombinedChildren = true;
        [HideInInspector] public bool deactivateCombinedChildrenMeshRenderers = true;
        [HideInInspector] public bool destroyCombinedChildren = false;


        [Button("Optimize Meshes")]
        public void StartCombine()
        {

            if (this.GetComponent<MeshFilter>() != null)
            {
#if UNITY_EDITOR
                EditorUtility.DisplayDialog("Warning",
                    "The gameobject with PerformanceOptimizer on it must have no Mesh attached to it", "OK");
#endif
                return;
            }

            CombineMeshes(true);
        }

        // Extension:
        [Button("Undo Mesh optimize")]
        public void StartUndoCmobine()
        {
            undoMeshCombine();
        }

        public void CombineMeshes(bool showCreatedMeshInfo)
        {
            #region Save our parent scale and our Transform and reset it temporarily:

#if UNITY_EDITOR
            if (DeleteCombinedMeshes)
                if (!EditorUtility.DisplayDialog("Warning",
                    "Are you sure that you want to delete the original meshes - this is not reversible", "OK",
                    "No - abort"))
                    return;
#endif
            // When we are unparenting and get parent again then sometimes scale is a little bit different so save scale before unparenting:
            Vector3 oldScaleAsChild = transform.localScale;

            // If we have parent then his scale will affect to our new combined Mesh scale so unparent us:
            int positionInParentHierarchy = transform.GetSiblingIndex();
            Transform parent = transform.parent;
            transform.parent = null;

            // Thanks to this the new combined Mesh will have same position and scale in the world space like its children:
            Quaternion oldRotation = transform.rotation;
            Vector3 oldPosition = transform.position;
            Vector3 oldScale = transform.localScale;
            transform.rotation = Quaternion.identity;
            transform.position = Vector3.zero;
            transform.localScale = Vector3.one;

            #endregion Save Transform and reset it temporarily.

            #region Combine Meshes into one Mesh:

            CombineMeshesWithMutliMaterial(showCreatedMeshInfo);

            #endregion Combine Meshes into one Mesh.

            #region Set old Transform values:

            // Bring back the Transform values:
            transform.rotation = oldRotation;
            transform.position = oldPosition;
            transform.localScale = oldScale;

            // Get back parent and same hierarchy position:
            transform.parent = parent;
            transform.SetSiblingIndex(positionInParentHierarchy);

            // Set back the scale value as child:
            transform.localScale = oldScaleAsChild;

            #endregion Set old Transform values.
        }

        private MeshFilter[] GetMeshFiltersToCombine()
        {
            Transform oldparent = null;
            // Get all MeshFilters belongs to this GameObject and its children:
            var allmeshFilters = GetComponentsInChildren<MeshFilter>(true);
            if (ExcludeGroups)
            {
                // temporarily change parent
                oldparent = this.gameObject.transform.parent;
                this.gameObject.transform.parent = null;
            }

            List<MeshFilter> MeshFiltersToUse = new List<MeshFilter>();
            foreach (var meshFilter in allmeshFilters)
            {
                bool skip = false;
                if (meshFilter.gameObject == this.gameObject)
                    skip = true;

                if (!IncludeNonStaticElements)
                    if (meshFilter.gameObject.isStatic == false)
                        skip = true;

                if (ExcludeGroups)
                {
                    var currParentGr = meshFilter.gameObject.GetComponentInParent<Group>(true);
                    if (currParentGr != null)
                    {
                        skip = true;

                        // there might be 2 Groups in Parent
                        var groups = currParentGr.gameObject.GetComponents<Group>();
                        foreach (var group in groups)
                        {
                            if (IncludedGroupsForMeshOptimizer != null)
                                if (IncludedGroupsForMeshOptimizer.Contains(group.GroupName))
                                    skip = false;
                        }
                    }
                }

                if (!skip)
                    MeshFiltersToUse.Add(meshFilter);
            }

            if (ExcludeGroups)
            {
                // temporarily change parent
                this.gameObject.transform.parent = oldparent;
            }

            return MeshFiltersToUse.ToArray();
        }

        private void CombineMeshesWithMutliMaterial(bool showCreatedMeshInfo)
        {

            var a = GetChildByName("OptimizedMesh");
            if (a != null)
                DestroyImmediate(a);
            MeshFilter[] meshFilters = GetMeshFiltersToCombine();
            if (meshFilters.Length == 0)
                return;
            MeshRenderer[] meshRenderers = new MeshRenderer[meshFilters.Length + 1];
            if (meshFilters.Length == 0)
                Debug.Log("Nothing to combine!");
            GameObject BaseGo = null;

            if (!CreateSubObjectForMesh)
                BaseGo = this.gameObject;
            else
                BaseGo = Global.AddGameObjectIfNotExisting("OptimizedMesh", this.gameObject);

            MeshFilter filter = null;
            filter = BaseGo.GetComponent<MeshFilter>();
            if (filter == null)
                filter = BaseGo.AddComponent<MeshFilter>();
            meshRenderers[0] = Global.AddComponentIfNotExisting<MeshRenderer>(BaseGo);
            List<Material> uniqueMaterialsList = new List<Material>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                meshRenderers[i + 1] = meshFilters[i].GetComponent<MeshRenderer>();
                if (meshRenderers[i + 1] != null)
                {
                    Material[] materials = meshRenderers[i + 1].sharedMaterials; // Get all Materials from child Mesh.
                    for (int j = 0; j < materials.Length; j++)
                    {
                        if (!uniqueMaterialsList
                            .Contains(materials[j])) // If Material doesn't exists in the list then add it.
                        {
                            uniqueMaterialsList.Add(materials[j]);
                        }
                    }
                }
            }

            List<CombineInstance> finalMeshCombineInstancesList = new List<CombineInstance>();

            // If it will be over 65535 then use the 32 bit index buffer:
            long verticesLength = 0;

            for (int i = 0;
                i < uniqueMaterialsList.Count;
                i++) // Create each Mesh (submesh) from Meshes with the same Material.
            {
                List<CombineInstance> submeshCombineInstancesList = new List<CombineInstance>();

                for (int j = 0; j < meshFilters.Length; j++) // Get only childeren Meshes (skip our Mesh).
                {
                    if (meshRenderers[j + 1] != null)
                    {
                        Material[] submeshMaterials =
                            meshRenderers[j + 1].sharedMaterials; // Get all Materials from child Mesh.

                        for (int k = 0; k < submeshMaterials.Length; k++)
                        {
                            // If Materials are equal, combine Mesh from this child:
                            if (uniqueMaterialsList[i] == submeshMaterials[k])
                            {
                                CombineInstance combineInstance = new CombineInstance();
                                combineInstance.subMeshIndex = k; // Mesh may consist of smaller parts - submeshes.
                                // Every part have different index. If there are 3 submeshes
                                // in Mesh then MeshRender needs 3 Materials to render them.
                                combineInstance.mesh = meshFilters[j].sharedMesh;
                                combineInstance.transform = meshFilters[j].transform.localToWorldMatrix;
                                submeshCombineInstancesList.Add(combineInstance);
                                verticesLength += combineInstance.mesh.vertices.Length;
                            }
                        }
                    }
                }

                // Create new Mesh (submesh) from Meshes with the same Material:
                Mesh submesh = new Mesh();

                if (verticesLength > Mesh16BitBufferVertexLimit)
                {
                    submesh.indexFormat =
                        UnityEngine.Rendering.IndexFormat.UInt32; // Only works on Unity 2017.3 or higher.
                }

                submesh.CombineMeshes(submeshCombineInstancesList.ToArray(), true);
                CombineInstance finalCombineInstance = new CombineInstance();
                finalCombineInstance.subMeshIndex = 0;
                finalCombineInstance.mesh = submesh;
                finalCombineInstance.transform = Matrix4x4.identity;
                finalMeshCombineInstancesList.Add(finalCombineInstance);
            }

            meshRenderers[0].sharedMaterials = uniqueMaterialsList.ToArray();

            Mesh combinedMesh = new Mesh();
            combinedMesh.name = name;
            if (verticesLength > Mesh16BitBufferVertexLimit)
            {
                combinedMesh.indexFormat =
                    UnityEngine.Rendering.IndexFormat.UInt32;
            }

            combinedMesh.CombineMeshes(finalMeshCombineInstancesList.ToArray(), false);
            MeshFilter Base = BaseGo.GetComponent<MeshFilter>();
            Base.sharedMesh = combinedMesh;


            filter.gameObject.isStatic = CreateStaticMesh;
            DeactivateCombinedGameObjects(meshFilters, BaseGo);
            if (DeleteCombinedMeshes)
                DeleteCombined(meshFilters, BaseGo);


            if (showCreatedMeshInfo)
            {
                if (verticesLength <= Mesh16BitBufferVertexLimit)
                {
                    Debug.Log("<color=#00cc00><b>Mesh \"" + name + "\" was created from " + (meshFilters.Length - 1) +
                              " children meshes and has "
                              + finalMeshCombineInstancesList.Count + " submeshes, and " + verticesLength +
                              " vertices.</b></color>");
                }
                else
                {
                    Debug.Log("<color=#ff3300><b>Mesh \"" + name + "\" was created from " + (meshFilters.Length - 1) +
                              " children meshes and has "
                              + finalMeshCombineInstancesList.Count + " submeshes, and " + verticesLength
                              + " vertices. Some old devices, like Android with Mali-400 GPU, do not support over 65535 vertices.</b></color>");
                }
            }
        }

        private void DeleteCombined(MeshFilter[] meshFilters, GameObject exclude)
        {
            foreach (var meshfilter in meshFilters)
            {
                // Check for empty upper gameobjects
                bool parentempty = true;
                List<GameObject> ParentsToDelete = new List<GameObject>();
                if (meshfilter != null)
                {
                    Transform currparent = meshfilter.gameObject.transform.parent;
                    while (parentempty && currparent != null)
                    {
                        if (currparent.GetComponent<MeshFilter>() == null && currparent.transform.childCount == 1)
                        {
                            ParentsToDelete.Add(currparent.gameObject);
                            currparent = currparent.transform.parent;
                        }
                        else
                        {
                            parentempty = false;
                        }
                    }

                    if (meshfilter.gameObject != exclude)
                        DestroyImmediate(meshfilter.gameObject);
                    foreach (var parent in ParentsToDelete.ToArray())
                    {
                        if (parent != exclude)
                            DestroyImmediate(parent);
                    }
                }
            }
        }

        private void DeactivateCombinedGameObjects(MeshFilter[] meshFilters, GameObject exclude)
        {
            for (int i = 0;
                i < meshFilters.Length - 1;
                i++) // Skip first MeshFilter belongs to this GameObject in this loop.
            {
                if (meshFilters[i + 1].gameObject != exclude)
                {
                    if (!destroyCombinedChildren)
                    {
                        if (deactivateCombinedChildren)
                        {
                            meshFilters[i + 1].gameObject.SetActive(false);
                        }

                        if (deactivateCombinedChildrenMeshRenderers)
                        {
                            MeshRenderer meshRenderer = meshFilters[i + 1].gameObject.GetComponent<MeshRenderer>();
                            if (meshRenderer != null)
                            {
                                meshRenderer.enabled = false;
                            }
                        }
                    }
                }
            }
        }


        private void undoMeshCombine()
        {
            if (CreateSubObjectForMesh)
            {
                GameObject GOMesh = GetChildByName("OptimizedMesh");
                if (GOMesh != null)
                    DestroyImmediate(GOMesh);
            }
            else
            {
                var renderer = GetComponent<MeshRenderer>();
                if (renderer != null) DestroyImmediate(renderer);
                var filter = GetComponent<MeshFilter>();
                if (filter != null) DestroyImmediate(filter);
            }

            MeshFilter[] meshFilters = GetMeshFiltersToCombine();
            activateCombinedGameObjects(meshFilters);
        }


        private void activateCombinedGameObjects(MeshFilter[] meshFilters)
        {
            for (int i = 0;
                i < meshFilters.Length;
                i++)
            {
                meshFilters[i].gameObject.SetActive(true);
                MeshRenderer meshRenderer = meshFilters[i].gameObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = true;
                }
            }
        }
    }
}