// Game4Automation (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2019 in2Sight GmbH - Usage of this source code only allowed based on License conditions see https://game4automation.com/lizenz  

   using System;
   using System.Collections.Generic;
   using UnityEngine;
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN ) && !UNITY_WEBGL
using SpaceNavigatorDriver;
#endif
#if CINEMACHINE
   using Cinemachine;
#endif
using NaughtyAttributes;
   using UnityEditor;


namespace game4automation
{
    public class SceneMouseNavigation : Game4AutomationBehavior
    {
        public bool UseOrbitCameraMode = false;
        public bool FirstPersonControllerActive = true;
        public bool RotateWithLeftMouseButton = false;
        public FirstPersonController FirstPersonController;
        public CameraPos LastCameraPosition;
        public bool SetCameraPosOnStartPlay = true;
        public bool SaveCameraPosOnQuit = true;
        public bool SetEditorCameraPos= true;
        public Transform target;
        public Vector3 targetOffset;
        public float distance = 5.0f;
        public bool CalclulateMaxDistanceWithBoundingBox = true;
        [HideIf("CalclulateMaxDistanceWithBoundingBox")] public float maxDistance = 20;
     
        public float minDistance = .6f;
        public float xSpeed = 200.0f;
        public float ySpeed = 200.0f;
        public int yMinLimit = -80;
        public int yMaxLimit = 80;
        public int zoomRate = 40;
        public float panSpeed = 0.3f;
        public float orthoPanSpeed = 0.3f;
        public float zoomDampening = 5.0f;
        public float StartDemoOnInactivity = 5.0f;
        public float DurationNoMouseActivity = 0;
        public GameObject DebugObj;

        [Header("Touch")] public TouchInteraction Touch;
        public float TouchPanSpeed = 200f;
        public float TouchRotationSpeed = 200f;
        public float TouchTiltSpeed = 200f;
        public float TouchZoomSpeed = 10f;

        [Header("SpaceNavigator")] 
        public bool EnableSpaceNavigator = true;
        public float SpaceNavTransSpeed = 1;
        private float xDeg = 0.0f;
        private float yDeg = 0.0f;
        [Header("Distance and Rotation")] 
        public float currentDistance;
        public float desiredDistance;
        public Quaternion currentRotation;
        public Quaternion desiredRotation;
        private Quaternion rotation;
        private Vector3 position;
        private Camera mycamera;
        private float _lastmovement;
        private bool _demostarted;
        private float lastperspectivedistance;
        private Vector3 _pos;
        private bool touch;
        private bool startcameraposset = false;
        [HideInInspector] public bool orthograhicview = false;
        [HideInInspector] public OrthoViewController orthoviewcontroller;
        public bool CinemachineIsActive = false;
        private bool selectionmanagernotnull = false;
        private SelectionRaycast selectionmanager;
        private GameObject selectedbefore;
        public void OnButtonOrthoOverlay(GenericButton button)
        {
            orthoviewcontroller.OrthoEnabled = button.IsOn;
            orthoviewcontroller.UpdateViews();
        }


        public void OnButtonOrthographicView(GenericButton button)
        {
            SetOrthographicView(button.IsOn);
        }
        
        public void SetOrthographicView(bool active)
        {
            if (active == orthograhicview && Application.isPlaying)
                return; /// no changes
            orthograhicview = active;
            if (mycamera == null)
                mycamera = GetComponent<Camera>();
            mycamera.orthographic = active;
            if (!active)
            {
                desiredDistance = lastperspectivedistance;
                mycamera.farClipPlane = 5000f;
                mycamera.nearClipPlane = 0.1f; 
            }
            else
            {
                lastperspectivedistance = desiredDistance;
                mycamera.farClipPlane = 5000f;
                mycamera.nearClipPlane = -5000f;
            }
            
            // change button in UI
            var button = Global.GetComponentByName<GenericButton>(Global.g4acontroller.gameObject, "Perspective");
            if (button != null)
                if (button.IsOn != active)
                    button.SetStatus(active);

        }
        
        void Start()
        {
            selectionmanagernotnull = GetComponent<SelectionRaycast>() != null;
            if (selectionmanagernotnull)
                selectionmanager = GetComponent<SelectionRaycast>();
            if (LastCameraPosition!=null)
                if (SetCameraPosOnStartPlay)
                    LastCameraPosition.SetCameraPositionPlaymode(this);
            
 #if UNITY_WEBGL
            RotateWithLeftMouseButton = true;
#endif
        }

        void OnEnable()
        {
            Touch.oneTouchPanEvent += OneFingerMoveHandler;
            Touch.twoTouchPanZoomRotDelegate += TwoFingerTransformHandler;
            Touch.threeTouchPanDelegate += ThreeFingerMoveHandler;
            Init();

        }


        private void OnApplicationQuit()
        {
            if (LastCameraPosition!=null)
                if (SaveCameraPosOnQuit)
                     LastCameraPosition.SaveCameraPosition(this);
        }
        
        private void OnDisable()
        {
            Touch.oneTouchPanEvent -= OneFingerMoveHandler;
            Touch.twoTouchPanZoomRotDelegate -= TwoFingerTransformHandler;
            Touch.threeTouchPanDelegate = ThreeFingerMoveHandler;
        }

        public void OnViewButton(GenericButton button)
        {
            if (button.IsOn && FirstPersonController != null)
            {
                SetOrthographicView(false);
                if (CinemachineIsActive)
                    ActivateCinemachine(false);
                Global.SetActiveIncludingSubObjects(FirstPersonController.gameObject,true);
                FirstPersonControllerActive = true;
                FirstPersonController.SetActive(true);

            }
            else
            {
                FirstPersonControllerActive = false;
                Global.SetActiveIncludingSubObjects(FirstPersonController.gameObject,false);
                
            }
        }
        

        private void MoveCam(Vector3 deltatrans, Vector3 deltarot, float deltadistance)
        {
            // Set Values
            target.rotation = transform.rotation;
            currentDistance = currentDistance + deltadistance;
            desiredDistance = currentDistance;
            target.transform.Translate(deltatrans);
            transform.Rotate(deltarot);
            rotation = transform.rotation;
            position = target.position - (rotation * Vector3.forward * currentDistance + targetOffset);
            desiredDistance = currentDistance; 
            transform.position = position;
         
            touch = true;

        }
        private void OneFingerMoveHandler(Vector2 pos, Vector2 pan)
        {
          
            Vector3 trans = new Vector3(-pan.x/Screen.width,pan.y/Screen.height,0);
            var targetdeltatrans = trans*TouchPanSpeed*6;
            MoveCam(targetdeltatrans, new Vector3(0,0,0 ), 0 );
    
        }

       private void ThreeFingerMoveHandler(Vector2 pos, Vector2 pan)
        {
            Vector3 trans =  new Vector3(-pan.x/Screen.width,pan.y/Screen.height,0);
            // set camera rotation 
            var rot = new Vector3 (-trans.y*TouchTiltSpeed*100, -trans.x*TouchTiltSpeed*100, 0);
            MoveCam(new Vector3(0,0,0), rot, 0 );
        }
        
        private void TwoFingerTransformHandler(Vector2 pos, Vector2 pan, float zoom, float rot)
        {
            var targetdeltatrans = new Vector3(0,0,0);
            var camdeltarot = new Vector3(0,0,0);
            // translation
           
            Vector3 trans = new Vector3(-pan.x/Screen.width,pan.y/Screen.height,0);
            targetdeltatrans = trans*TouchPanSpeed*6;
           
            // Rotation
            camdeltarot = new Vector3(0,0,rot*TouchRotationSpeed*2);

            // Scale
            var deltadistance = -zoom/Screen.width*TouchZoomSpeed*20;
           
            // Set Values
            MoveCam(targetdeltatrans,camdeltarot, deltadistance);    
    
        } 

        public void SetNewCameraPosition(Vector3 targetpos, float camdistance, Vector3 camrotation)
        {
            // End first person controller if it is on
            if (FirstPersonControllerActive)
            {
                FirstPersonController.SetActive(false);
                FirstPersonControllerActive = false;
            }
            if (target == null)
                return;
            desiredDistance = camdistance;
            currentDistance = camdistance;
            target.position = targetpos;
            desiredRotation = Quaternion.Euler(camrotation);
            currentRotation = Quaternion.Euler(camrotation);
            rotation = Quaternion.Euler(camrotation);
            transform.rotation = Quaternion.Euler(camrotation);
        }
        
        
        public void SetViewDirection(Vector3 camrotation)
        {
            
            desiredRotation = Quaternion.Euler(camrotation);
            currentRotation = Quaternion.Euler(camrotation);
            rotation = Quaternion.Euler(camrotation);
            transform.rotation = Quaternion.Euler(camrotation);
        }

        public void ActivateCinemachine(bool activate)
        {
#if CINEMACHINE
            CinemachineBrain brain;
            brain = GetComponent<CinemachineBrain>();
            if (brain == null)
                return;
            
            if (!activate)
            {
                if (brain.ActiveVirtualCamera != null)
                {
                    Quaternion camrot = brain.ActiveVirtualCamera.VirtualCameraGameObject.transform.rotation;
                    Vector3 rot = camrot.eulerAngles;
                    distance = Vector3.Distance(transform.position, target.position);
                    Vector3 tarpos = brain.ActiveVirtualCamera.VirtualCameraGameObject.transform.position +
                                     (camrot * Vector3.forward * distance + targetOffset);
                    SetNewCameraPosition(tarpos, distance, rot);
                }
            }
            if (brain != null)
            {
                if (activate)
                {
                    brain.enabled = true;

                }
                else
                {
                    brain.enabled = false;

                }
            }

            CinemachineIsActive = activate;
         
                
#endif
        }

        #if CINEMACHINE
        public void ActivateCinemachineCam(CinemachineVirtualCamera vcam)
        {
            vcam.enabled = true;
            vcam.Priority = 100;
            if (CinemachineIsActive==false)
                ActivateCinemachine(true);
            
            // Set low priority to all other vcams
            var vcams = GameObject.FindObjectsOfType(typeof(CinemachineVirtualCamera));
            foreach (CinemachineVirtualCamera vc in vcams)
            {
                if (vc != vcam)
                    vc.Priority = 10;
            }
        }
        #endif
        
        public void Init()
        {
#if CINEMACHINE
            ActivateCinemachine(false);
          
#endif
            if (CalclulateMaxDistanceWithBoundingBox)
            {
                var rnds = FindObjectsOfType<Renderer>();
                if (rnds.Length != 0)
                {
                    var b = rnds[0].bounds;
                    for (int i = 1; i < rnds.Length; i++)
                    {
                        b.Encapsulate(rnds[i].bounds);
                    
                    }

                    maxDistance = b.size.magnitude * 1.5f;
                }
            }
            
            //If there is no target, create a temporary target at 'distance' from the cameras current viewpoint
            if (!target)
            {
                GameObject go = new GameObject("Cam Target");
                go.transform.position = transform.position + (transform.forward * distance);
                target = go.transform;
            }

            mycamera = GetComponent<Camera>();

            distance = Vector3.Distance(transform.position, target.position);
            currentDistance = distance;
            desiredDistance = distance;

            //be sure to grab the current rotations as starting points.
            position = transform.position;
            rotation = transform.rotation;
            currentRotation = transform.rotation;
            desiredRotation = transform.rotation;

            xDeg = Vector3.Angle(Vector3.right, transform.right);
            yDeg = Vector3.Angle(Vector3.up, transform.up);


            if (LastCameraPosition != null && !FirstPersonControllerActive && !startcameraposset)
            {
                if (SetCameraPosOnStartPlay)
                {
                    SetNewCameraPosition(LastCameraPosition.TargetPos, LastCameraPosition.CameraDistance, LastCameraPosition.CameraRot);
                }
                startcameraposset = true;
            }
         
         

            if (FirstPersonController != null)
            {
                if (FirstPersonControllerActive)
                {
                    FirstPersonController.SetActive(true);
                }
                else
                {
                    FirstPersonController.SetActive(false);
                }
            }


            orthoviewcontroller = this.transform.parent.GetComponentInChildren<OrthoViewController>();

        }

        void CameraTransform(Vector3 direction)
        {
            target.rotation = transform.rotation;
            target.Translate(direction * panSpeed);
            _lastmovement = Time.realtimeSinceStartup;
        }

        void CamereSetDirection(Vector3 direction)
        {
            desiredDistance = 10f;
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }

        bool MouseOverViewport(Camera main_cam, Camera local_cam)
        {
            if (!Input.mousePresent) return true; //always true if no mouse??
 
            Vector3 main_mou = main_cam.ScreenToViewportPoint (Input.mousePosition);
            return local_cam.rect.Contains (main_mou);
        }
        
    
        /*
     * Camera logic on LateUpdate to only update after all character movement logic has been handled. 
     */
        void LateUpdate()
        {
            var buttonrotate = 1;
            if (RotateWithLeftMouseButton)
                buttonrotate = 0;
            

            bool MouseInOrthoCamera = false;
            Camera incamera = mycamera;
            if (Camera.allCameras.Length>1)
            foreach (var cam in Camera.allCameras)
            {
                if (cam != Camera.main)
                {
                    if (MouseOverViewport(mycamera, cam))
                    {
                        MouseInOrthoCamera = true;
                        incamera = cam;
                    }
                        
                }
            }

          
            if (FirstPersonControllerActive)
                return;

            if (UseOrbitCameraMode)
                return;
          
            if (CinemachineIsActive)
            {
                var scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Input.GetMouseButton(2) || Input.GetMouseButton(3) || Input.GetMouseButton(1) 
                    || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.LeftArrow) ||
                    Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.UpArrow) || Math.Abs(scroll)>0.001f||  Input.GetKey(KeyCode.Escape))
                {
                    ActivateCinemachine(false);
                }
            }

            
       
            // If Control and Middle button? ZOOM!
            if (Input.GetMouseButton(2) && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && !touch)
            {
                _lastmovement = Time.realtimeSinceStartup;
                desiredDistance -= Input.GetAxis("Mouse Y") * Time.deltaTime * zoomRate * 0.125f *
                                   Mathf.Abs(desiredDistance);
         
            }
            // If right mous is selected ORBIT
           else if (Input.GetMouseButton(buttonrotate) && !touch)
            {
                _lastmovement = Time.realtimeSinceStartup;
                xDeg += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                yDeg -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

                ////////OrbitAngle

                //Clamp the vertical axis for the orbit
                yDeg = ClampAngle(yDeg, yMinLimit, yMaxLimit);
                // set camera rotation 
                desiredRotation = Quaternion.Euler(yDeg, xDeg, 0);
                currentRotation = transform.rotation;

                rotation = Quaternion.Lerp(currentRotation, desiredRotation, Time.deltaTime * zoomDampening);
                transform.rotation = rotation;
            }
            // otherwise if middle mouse is selected, we pan by way of transforming the target in screenspace*/
            else if (Input.GetMouseButton(2) && !touch)
            {
                if (!MouseInOrthoCamera)
                {
                    _lastmovement = Time.realtimeSinceStartup;
                    //grab the rotation of the camera so we can move in a psuedo local XY space
                    target.rotation = transform.rotation;
                    target.Translate(Vector3.right * -Input.GetAxis("Mouse X") * panSpeed);
                    target.Translate(transform.up * -Input.GetAxis("Mouse Y") * panSpeed, Space.World);
                }
                else
                {
                    if (orthoviewcontroller != null)
                    {
                        if (incamera.name == "Side")
                        {
                            orthoviewcontroller.transform.Translate(Vector3.right * -Input.GetAxis("Mouse X") *
                                orthoPanSpeed * orthoviewcontroller.Distance / 10);
                            orthoviewcontroller.transform.Translate(Vector3.up * -Input.GetAxis("Mouse Y") *
                                orthoPanSpeed * orthoviewcontroller.Distance / 10);
                        }

                        if (incamera.name == "Top")
                        {
                            orthoviewcontroller.transform.Translate(new Vector3(0, 0, -1) * -Input.GetAxis("Mouse X") *
                                orthoPanSpeed * orthoviewcontroller.Distance / 10);
                            orthoviewcontroller.transform.Translate(new Vector3(1, 0, 0) * -Input.GetAxis("Mouse Y") *
                                orthoPanSpeed * orthoviewcontroller.Distance / 10);
                        }
                        
                        if (incamera.name == "Front")
                        {
                            orthoviewcontroller.transform.Translate(new Vector3(0, 0, -1) * -Input.GetAxis("Mouse X") *
                                orthoPanSpeed * orthoviewcontroller.Distance / 10);
                            orthoviewcontroller.transform.Translate(new Vector3(0, 1, 0) * -Input.GetAxis("Mouse Y") *
                                orthoPanSpeed * orthoviewcontroller.Distance / 10);
                        }
                    }
                }
            }

            ////////Orbit Position

            // affect the desired Zoom distance if we roll the scrollwheel
            var mousescroll = Input.GetAxis("Mouse ScrollWheel");
            if (mousescroll > 0)
                _lastmovement = Time.realtimeSinceStartup;
            if (!MouseInOrthoCamera)
            {
                 desiredDistance -= mousescroll * Time.deltaTime * zoomRate * Mathf.Abs(desiredDistance);
            }
            else
            {
                if (orthoviewcontroller != null)
                {
                    orthoviewcontroller.Distance += mousescroll * orthoviewcontroller.Distance;
                    orthoviewcontroller.UpdateViews();
                }
            }
            
            //clamp the zoom min/max
            desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
            
            // if hotkey focus is pressed and selectionamanger has a selected object, focus it
            if (selectionmanagernotnull)
            {
                if ((Input.GetKey(KeyCode.F) || selectionmanager.DoubleSelect) && selectionmanager.SelectedObject != null)
                {
                    _lastmovement = Time.realtimeSinceStartup;
                    var pos = selectionmanager.GetHitpoint();
                    selectionmanager.ShowCenterIcon(true);
                    // get bounding box of selected object
                    var bounds = selectionmanager.SelectedObject.GetComponentInChildren<Renderer>().bounds;
                    var mycam = GetComponent<Camera>();

                    float cameraDistance = 2.0f; 
                    Vector3 objectSizes = bounds.max - bounds.min;
                    float objectSize = Mathf.Max(objectSizes.x, objectSizes.y, objectSizes.z);
                    float cameraView = 2.0f * Mathf.Tan(0.5f * Mathf.Deg2Rad * Camera.main.fieldOfView); // Visible height 1 meter in front
                    float distance = cameraDistance * objectSize / cameraView; // Combined wanted distance from the object
                    distance += 0.5f * objectSize; // Estimated offset from the center to the outside of the object
                    target.position = pos;
                    desiredDistance = distance;
                }

                if (selectionmanager.SelectedObject != null && ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) || selectionmanager.AutoCenterSelectedObject))
                {
                    var pos = selectionmanager.GetHitpoint();
                    selectionmanager.ShowCenterIcon(true);
                    target.position = pos;
                }
            }
        

            if (!MouseInOrthoCamera)
            {
                // Key Navigation
                var shift = false;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    shift = true;
                var control = false;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    control = true;
                // Key 3D Navigation
                if (Input.GetKey(KeyCode.UpArrow) && shift && !control)
                    CameraTransform(Vector3.forward);

                if (Input.GetKey(KeyCode.DownArrow) && shift && !control)
                    CameraTransform(Vector3.back);

                if (Input.GetKey(KeyCode.UpArrow) && !shift && !control)
                    CameraTransform(Vector3.down);

                if (Input.GetKey(KeyCode.DownArrow) && !shift && !control)
                    CameraTransform(Vector3.up);

                if (Input.GetKey(KeyCode.RightArrow) && !control)
                    CameraTransform(Vector3.left);

                if (Input.GetKey(KeyCode.LeftArrow) && !control)
                    CameraTransform(Vector3.right);

                if (Input.GetKey(KeyCode.LeftArrow) && control)
                    CamereSetDirection(Vector3.left);

                if (Game4AutomationController.EnableHotkeys)
                {
               
                    if (Input.GetKey(Game4AutomationController.HotKeyTopView))
                    {    
                        SetViewDirection(new Vector3(90,90,0));
         
                    }
                    if (Input.GetKey(Game4AutomationController.HotKeyFrontView))
                    {
                        if (selectionmanagernotnull && Game4AutomationController.HotKeyFrontView == Game4AutomationController.HoteKeyFocfus)
                            if (selectionmanager.SelectedObject == null)
                                 SetViewDirection(new Vector3(0, 90, 0));
                            else
                                SetViewDirection(new Vector3(0, 90, 0));
      
                    }
                    if (Input.GetKey(Game4AutomationController.HotKeyBackView))
                    {
                        SetViewDirection(new Vector3(0,180,0));
        
                    }
                    if (Input.GetKey(Game4AutomationController.HotKeyLeftView))
                    {
                        SetViewDirection(new Vector3(0,180,0));
      
                    }
                    if (Input.GetKey(Game4AutomationController.HotKeyRightView))
                    {
                        SetViewDirection(new Vector3(0,0,0));
        
                    }
                }
             
            }
            else
            {
                if (Game4AutomationController.EnableHotkeys)
                {
                    if (Input.GetKeyDown(Game4AutomationController.HotKeyOrhtoBigger))
                        orthoviewcontroller.Size += 0.05f;
                    if (Input.GetKeyDown(Game4AutomationController.HotKeyOrhtoSmaller))
                        orthoviewcontroller.Size -= 0.05f;
                    if (orthoviewcontroller.Size > 0.45f)
                        orthoviewcontroller.Size = 0.45f;
                    if (orthoviewcontroller.Size < 0.1f)
                        orthoviewcontroller.Size = 0.1f;
                    if (Input.GetKeyDown(Game4AutomationController.HoteKeyOrthoDirection))
                        orthoviewcontroller.Angle += 90;
                    if (orthoviewcontroller.Angle >= 360)
                        orthoviewcontroller.Angle = 0;
                    orthoviewcontroller.UpdateViews();
                }
            }
   
            if (Game4AutomationController.EnableHotkeys)
                if (Input.GetKeyDown(Game4AutomationController.HotKeyOrthoViews))
                {
                    orthoviewcontroller.OrthoEnabled = !orthoviewcontroller.OrthoEnabled;
                    var button =
                        Global.GetComponentByName<GenericButton>(Game4AutomationController.gameObject, "OrthoViews");
                    if (button!=null)
                        button.SetStatus(orthoviewcontroller.OrthoEnabled);
                    orthoviewcontroller.UpdateViews();
                }
                
            if (mycamera.orthographic)
            {
                mycamera.orthographicSize += mousescroll * mycamera.orthographicSize;
                desiredDistance = 0;
            }

#if ((!UNITY_IOS && !UNITY_ANDROID &&! UNITY_EDITOR_OSX && !UNITY_WEBGL) || (UNITY_EDITOR && !UNITY_WEBGL))
            // Space Navigator
            if (EnableSpaceNavigator)
            {
                if (SpaceNavigator.Translation != Vector3.zero)
                {
                    target.rotation = transform.rotation;
                    var spacetrans = SpaceNavigator.Translation;
                    var newtrans = new Vector3(-spacetrans.x, spacetrans.y, -spacetrans.z) * SpaceNavTransSpeed;
                    target.Translate(newtrans, Space.Self);
                }

                if (SpaceNavigator.Rotation.eulerAngles != Vector3.zero)
                {
                   
                    transform.Rotate(-SpaceNavigator.Rotation.eulerAngles);
                    rotation = transform.rotation;
                }
            }
#endif
       
           
            // For smoothing of the zoom, lerp distance
            currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * zoomDampening);

            // calculate position based on the new currentDistance 
            position = target.position - (rotation * Vector3.forward * currentDistance + targetOffset);
            if (position != transform.position)
            {
                transform.position = position;
            }

            touch = false;

            DurationNoMouseActivity = Time.realtimeSinceStartup - _lastmovement;
            #if CINEMACHINE
            if ((Time.realtimeSinceStartup - _lastmovement) > StartDemoOnInactivity)
            {
                if (!CinemachineIsActive)
                   ActivateCinemachine(true);
            }
            #endif
            
        }

        private static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360)
                angle += 360;
            if (angle > 360)
                angle -= 360;
            return Mathf.Clamp(angle, min, max);
        }
    }
}