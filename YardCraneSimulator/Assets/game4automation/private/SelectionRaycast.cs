
using System.Collections.Generic;
using NaughtyAttributes;
using RuntimeInspectorNamespace;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace game4automation
{
    
    [System.Serializable]
    public class Game4AutomationEventSelected: UnityEvent<GameObject, bool>
    {
    }

    [System.Serializable]
    public class Game4AutomationEventHovered: UnityEvent<GameObject, bool>
    {
    }
    
    


    //! Selection Raycast for selecting objects during runtime
    public class SelectionRaycast : Game4AutomationBehavior
    {
        public bool IsActive = true; //!<selection by raycast is active
        public bool ChangeMaterialOnHover = true; //!<change material on hover
        [ShowIf("ChangeMaterialOnHover")]public Material HighlightMaterial; //!<the highlight materiials
        public bool ChangeMaterialOnSelect = true; //!<change material on select
        [ShowIf("ChangeMaterialOnSelect")]public Material SelectMaterial;//!<the select material
        [ReorderableList] public List<string> SelectionLayer; //!<the layers that can be selected
        [ReadOnly] public GameObject SelectedObject;//!<the selected object
        [ReadOnly] public Vector3 SelectedPosition; //!<the selected object hit point position
        [ReadOnly] public GameObject HoveredObject;//!<the hovered object
        [ReadOnly] public Vector3 HoveredPosition;//!<the hovered object hit point position
        [ReadOnly] public bool DoubleSelect; //!<true if the object was double clicked
        public bool PingHoverObject; //!<true if the hovered object should be pinged in the hierarchy
        public bool SelectHoverObject; //!<true if the hovered object should be selected in the hierarchy
        public bool PingSelectObject; //!<true if the selected object should be pinged in the hierarchy
        public bool SelectSelectObject; //!<true if the selected object should be selected in the hierarchy
        public bool AutoCenterSelectedObject; //!<true if the selected object (its selection point) is automatically centered in the scene view
        public bool OpenRuntimeINspector;
        public bool ShowSelectedIcon;
        [Foldout("Events")] public Game4AutomationEventSelected
            EventSelected; //!<  Unity event which is called for MU enter and exit. On enter it passes MU and true. On exit it passes MU and false.
        [Foldout("Events")] public Game4AutomationEventHovered
            EventHovered; //!<  Unity event which is called for MU enter and exit. On enter it passes MU and true. On exit it passes MU and false.
        public RuntimeInspector RuntimeInspector;
        public GameObject SelectedIcon;

        private Vector3 Hitpoint;
        private Vector3 distancehitpoint;
        private RaycastHit GObject;
        private SceneMouseNavigation navigate;
        private int layermask;
        private int UILayer;
        private Camera camera;
        private List<ObjectSelection> selections = new List<ObjectSelection>();
        private List<ObjectSelection> hovers = new List<ObjectSelection>();
        private bool isactivebefore = false;
        private GameObject selectedicon;
        new void Awake()
        {
            base.Awake();

            if (!Game4AutomationController.ObjectSelectionEnabled)
            {
                return;
            }
            
            camera = GetComponent<Camera>();
            // get all meshrenderers
            var meshrenderers = FindObjectsOfType<MeshRenderer>();
           
            foreach (var comp in meshrenderers)
            {
                if (comp.gameObject.GetComponent<Collider>() == null)
                    {
                        // get mesh from gameobject
                        var collider = comp.gameObject.AddComponent<MeshCollider>();
                        collider.convex = true;
                        comp.gameObject.layer = LayerMask.NameToLayer(SelectionLayer[0]);
                    }
                else
                {
                    var collider = comp.GetComponent<Collider>();
                    // check if collider is on default layer
                    if (collider.gameObject.layer == 0)
                    {
                        // set layer to selection layer
                        collider.gameObject.layer = LayerMask.NameToLayer(SelectionLayer[0]);
                    }
                }
            }
            navigate = gameObject.GetComponent<SceneMouseNavigation>();
            base.Awake();
            UILayer = LayerMask.NameToLayer("UI");
        }

        //Returns 'true' if we touched or hovering on Unity UI element.
        public bool IsPointerOverUIElement()
        {
            return IsPointerOverUIElement(GetEventSystemRaycastResults());
        }
 
 
        //Returns 'true' if we touched or hovering on Unity UI element.
        private bool IsPointerOverUIElement(List<RaycastResult> eventSystemRaysastResults)
        {
            for (int index = 0; index < eventSystemRaysastResults.Count; index++)
            {
                RaycastResult curRaysastResult = eventSystemRaysastResults[index];
                if (curRaysastResult.gameObject.layer == UILayer)
                    return true;
            }
            return false;
        }
 
 
        //Gets all event system raycast results of current mouse or touch position.
        static List<RaycastResult> GetEventSystemRaycastResults()
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> raysastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raysastResults);
            return raysastResults;
        }

        
        public void HighlightHoverObject(bool highlight, GameObject currObj)
        {
            var meshrenderer = currObj.GetComponentInChildren<MeshRenderer>();
            if (highlight)
            {
                HoveredPosition = Hitpoint;
                if (ChangeMaterialOnHover)
                {
                    var mu = currObj.GetComponent<MU>();
                    if (mu != null)
                    {
                        var meshes = mu.GetComponentsInChildren<MeshRenderer>();
                        foreach (var mesh in meshes)
                        {
                            var sel = mesh.gameObject.AddComponent<ObjectSelection>();
                            sel.SetNewMaterial(HighlightMaterial);
                            hovers.Add(sel);
                        }
                    }
                    else
                    {
                        var sel = currObj.AddComponent<ObjectSelection>();
                        sel.SetNewMaterial(HighlightMaterial);
                        hovers.Add(sel);
                    }
                }

#if UNITY_EDITOR
                if (PingHoverObject)
                    EditorGUIUtility.PingObject((currObj));
                if (HoveredObject)
                    if (SelectHoverObject)
                        Selection.objects = new[] {currObj};
#endif
            }
            else
            {
                HoveredPosition = Vector3.zero;
                if (ChangeMaterialOnHover)
                {
                    foreach (var hover in hovers)
                    {
                        hover.ResetMaterial();
                    }
                    hovers.Clear();
                }
            
            }
        }

        public void HighlighSelectObject(bool highlight, GameObject currObj)
        {
            var mu = currObj.GetComponent<MU>();
           
            if (highlight)
            {
                
                
#if UNITY_EDITOR
                if (PingSelectObject)
                    EditorGUIUtility.PingObject((currObj));
                if (SelectSelectObject)
                    Selection.objects = new[] {currObj};
#endif
                if (!ChangeMaterialOnSelect)
                    return;

                if (mu != null)
                {
                    var meshes = mu.GetComponentsInChildren<MeshRenderer>();
                    foreach (var mesh in meshes)
                    {
                        var sel = mesh.gameObject.AddComponent<ObjectSelection>();
                        sel.SetNewMaterial(SelectMaterial);
                        selections.Add(sel);
                    }
                }
                else
                {
                    var sel = currObj.AddComponent<ObjectSelection>();
                    sel.SetNewMaterial(SelectMaterial);
                    selections.Add(sel);
                }

            

            }
            else
            {
                if (!ChangeMaterialOnSelect)
                    return;

                foreach (var sel in selections)
                {
                    sel.ResetMaterial();
                }
                selections.Clear();
            }
        }

        void OnSelected()
        {
            SelectedPosition = Hitpoint;
            distancehitpoint = SelectedObject.transform.position - Hitpoint;
            // Set an Icon at Hitpoint
        
            if (EventSelected != null) EventSelected.Invoke(SelectedObject,false);
            if (OpenRuntimeINspector)
            {
                // enable gameobject runtimeinspector
                if (RuntimeInspector != null && Game4AutomationController.RuntimeInspectorEnabled)
                {
                    RuntimeInspector.gameObject.SetActive(true);
                    RuntimeInspector.Inspect(SelectedObject);
                }
            }

        }

        void OnDeSeselected()
        {
         
            SelectedPosition = Vector3.zero;
            if (EventSelected != null) EventSelected.Invoke(SelectedObject,false);
            if (OpenRuntimeINspector)
            {
                if (RuntimeInspector != null && Game4AutomationController.RuntimeInspectorEnabled)
                {
                    RuntimeInspector.StopInspect();
                    RuntimeInspector.gameObject.SetActive(false);
                }
            }
            ShowCenterIcon(false);
               
        }
        
        public Vector3 GetHitpoint()
        {
            return SelectedObject.transform.position - distancehitpoint;
        }

        public void ShowCenterIcon(bool show)
        {
            if (show)
            {
                if (SelectedIcon != null && ShowSelectedIcon)
                {
                    if (selectedicon == null)
                        selectedicon = Instantiate(SelectedIcon, Hitpoint, Quaternion.identity);
                    selectedicon.transform.position = GetHitpoint();
                }
            }
            else
            {
                if (selectedicon != null)
                    DestroyImmediate(selectedicon);
            }
       
        }

        // Update is called once per frame
        void Update()
        {
            DoubleSelect = false;
            // turn component off if isactive = false
            if (!IsActive || !Game4AutomationController.ObjectSelectionEnabled)
            {
                // hide selected and hovered objects which have been selected before
                if (isactivebefore && !IsActive)
                {
                    if (SelectedObject != null)
                    {
                        HighlighSelectObject(false,SelectedObject);
                        OnDeSeselected();
                        SelectedObject = null;
                    }

                    if (HoveredObject != null)
                    {
                        HighlightHoverObject(false,HoveredObject);
                        HoveredObject = null;
                    }
                }
                isactivebefore = false;
                return;
            }

            isactivebefore = true;
            bool onUI = IsPointerOverUIElement();
            layermask = LayerMask.GetMask(SelectionLayer.ToArray());
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            var target =
                camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y,
                    camera.nearClipPlane));
            Debug.DrawRay(transform.position, -target, Color.red);
            GameObject mouseoverobj = null;
            if (Physics.Raycast(ray, out GObject, Mathf.Infinity, layermask) && !onUI)
            {
              
                Hitpoint = GObject.point;
                
                // Raycast is hitting
                var NewHoveredObject = GObject.transform.gameObject;

                // check if selectable in standard true
                var selectable = true;

                // only if gameobject is not in game4automationcontroller
                if (NewHoveredObject.transform.parent != null)
                {
                    if (NewHoveredObject.transform.parent == Game4AutomationController.gameObject.transform)
                        selectable = false;
                }

                if (NewHoveredObject.GetComponentInChildren<MeshRenderer>() == null)
                    selectable = false;

                /// only if it is not already hovered or not the selected object
                mouseoverobj = NewHoveredObject;
                if (!(NewHoveredObject != HoveredObject && NewHoveredObject != SelectedObject))
                {
                    selectable = false;
                }

                if (selectable)
                {
                    // Selected object is changing, unhighlight old, highlicht new
                    if (HoveredObject != null)
                        HighlightHoverObject(false, HoveredObject);
                    HighlightHoverObject(true, NewHoveredObject);
                    HoveredObject = NewHoveredObject;
                    if (EventHovered!=null) EventHovered.Invoke(HoveredObject,true);
                }
            }
            else
            {
                Hitpoint = Vector3.zero;
                
                // No raycast is hitting - deselect selected object
                if (HoveredObject != null)
                {
                    HighlightHoverObject(false, HoveredObject);
                    if (EventHovered!=null) EventHovered.Invoke(HoveredObject,false);
                }
                HoveredObject = null;
            }

            if (Input.GetMouseButtonDown(0) && !onUI)
            {
                if (HoveredObject != null)
                {
                    if (SelectedObject != HoveredObject)
                    {
                        if (SelectedObject != null)
                        {
                            OnDeSeselected();
                            HighlighSelectObject(false, SelectedObject);
                        }
                           
                        if (HoveredObject != null)
                            HighlightHoverObject(false, HoveredObject);
                        HighlighSelectObject(true, HoveredObject);
                        SelectedObject = HoveredObject;
                        OnSelected();
                        if (EventHovered != null) EventHovered.Invoke(HoveredObject,false);
                        HoveredObject = null;
                        
                    }
                }
                else
                {
                    if (mouseoverobj == SelectedObject)
                    {
                        OnSelected();
                        DoubleSelect = true;
                      
                    }
                    else
                    {
                        if (SelectedObject != null)
                        {
                            if (EventSelected!= null) EventSelected.Invoke(SelectedObject,false);
                            HighlighSelectObject(false, SelectedObject);
                            OnDeSeselected();
                            SelectedObject = null;
                          
                        }
                    }
                   
                }
            }
            

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (SelectedObject != null)
                {
                    if (EventSelected != null)
                        EventSelected.Invoke(SelectedObject,false);
                    HighlighSelectObject(false, SelectedObject);
                    OnDeSeselected();
                    HoveredObject = null;
                    SelectedObject = null;
                }
            }
        }
    }
}