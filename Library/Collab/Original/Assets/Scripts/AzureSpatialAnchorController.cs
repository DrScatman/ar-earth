// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    [RequireComponent(typeof(FirebaseLoader))]
    public class AzureSpatialAnchorController : DemoScriptBase
    {
        [SerializeField] private bool debug;
        [SerializeField] private Button saveButton;
        [SerializeField] private Text swipeUpText;
        [SerializeField] private Text swipeRightText;

        private FirebaseLoader _firebaseLoader;
        private bool isCreateMode;
        private bool isSavePressed;

        internal enum AppState
        {
            FirebaseInit = 0,
            DemoStepCreateSession,
            DemoStepConfigSession,
            DemoStepStartSession,
            DemoStepCreateLocationProvider,
            DemoStepConfigureSensors,
            DemoStepCheckAdminMode,
            DemoStepCreateLocalAnchor,
            DemoStepSaveCloudAnchor,
            DemoStepSavingCloudAnchor,
            DemoStepResetSession,
            DemoStepDestroySession,
            DemoStepCreateSessionForQuery,
            DemoStepStartSessionForQuery,
            DemoStepLookForAnchorsNearDevice,
            DemoStepLookingForAnchorsNearDevice,
            DemoStepStopWatcher,
            //******************************
            DemoStepStopSessionForQuery,
            DemoStepComplete
        }

        private readonly Dictionary<AppState, DemoStepParams> stateParams = new Dictionary<AppState, DemoStepParams>
        {
            { AppState.FirebaseInit,new DemoStepParams() { StepMessage = "Connecting to Firebase DB", StepColor = Color.clear }},
            { AppState.DemoStepCreateSession,new DemoStepParams() { StepMessage = "Next: Create Azure Spatial Anchors Session", StepColor = Color.clear }},
            { AppState.DemoStepConfigSession,new DemoStepParams() { StepMessage = "Next: Configure Azure Spatial Anchors Session", StepColor = Color.clear }},
            { AppState.DemoStepStartSession,new DemoStepParams() { StepMessage = "Next: Start Azure Spatial Anchors Session", StepColor = Color.clear }},
            //*Added from Coarse Reloc******
            { AppState.DemoStepCreateLocationProvider,new DemoStepParams() { StepMessage = "Next: Create Location Provider", StepColor = Color.clear }},
            { AppState.DemoStepConfigureSensors,new DemoStepParams() { StepMessage = "Next: Configure Sensors", StepColor = Color.clear }},
            //******************************
            { AppState.DemoStepCheckAdminMode,new DemoStepParams() { StepMessage = "Checking for admin mode", StepColor = Color.black }},
            { AppState.DemoStepCreateLocalAnchor,new DemoStepParams() { StepMessage = "Tap a Surface To Add A Model Location", StepColor = Color.white }},
            { AppState.DemoStepSaveCloudAnchor,new DemoStepParams() { StepMessage = "Press Button When Ready To Save", StepColor = Color.blue }},
            { AppState.DemoStepSavingCloudAnchor,new DemoStepParams() { StepMessage = "Saving Local Anchor To Cloud...", StepColor = Color.yellow }},
            { AppState.DemoStepResetSession,new DemoStepParams() { StepMessage = "Reset Azure Spatial Anchors Session", StepColor = Color.green }},
            { AppState.DemoStepCreateSessionForQuery,new DemoStepParams() { StepMessage = "Next: Create Azure Spatial Anchors Session for query", StepColor = Color.clear }},
            { AppState.DemoStepStartSessionForQuery,new DemoStepParams() { StepMessage = "Next: Start Azure Spatial Anchors Session for query", StepColor = Color.clear }},
            //Commenting out to test Coarse Reloc
            //{ AppState.DemoStepLookForAnchor,new DemoStepParams() { StepMessage = "Next: Look for Anchor", StepColor = Color.clear }},
            //{ AppState.DemoStepLookingForAnchor,new DemoStepParams() { StepMessage = "Looking for Anchor...", StepColor = Color.clear }},
            //{ AppState.DemoStepDeleteFoundAnchor,new DemoStepParams() { StepMessage = "Next: Delete Anchor", StepColor = Color.yellow }},
            //*Added from Coarse Reloc******
            { AppState.DemoStepLookForAnchorsNearDevice,new DemoStepParams() { StepMessage = "Next: Look for Anchors near device", StepColor = Color.clear }},
            { AppState.DemoStepLookingForAnchorsNearDevice,new DemoStepParams() { StepMessage = "Looking for Anchors near device...", StepColor = Color.clear }},
            { AppState.DemoStepStopWatcher,new DemoStepParams() { StepMessage = "Next: Stop Watcher", StepColor = Color.yellow }},
            //******************************
            { AppState.DemoStepStopSessionForQuery,new DemoStepParams() { StepMessage = "Next: Stop Azure Spatial Anchors Session for query", StepColor = Color.grey }},
            { AppState.DemoStepComplete,new DemoStepParams() { StepMessage = "Next: Restart demo", StepColor = Color.clear }}
        };

        private AppState _currentAppState = AppState.DemoStepCreateSession;

        AppState currentAppState
        {
            get
            {
                return _currentAppState;
            }
            set
            {
                if (_currentAppState != value)
                {
                    Debug.LogFormat("State from {0} to {1}", _currentAppState, value);
                    _currentAppState = value;
                    if (spawnedObjectMat != null)
                    {
                        spawnedObjectMat.color = stateParams[_currentAppState].StepColor;
                    }

                    if (!isErrorActive)
                    {
                        feedbackBox.text = stateParams[_currentAppState].StepMessage;
                        feedbackBox.color = stateParams[_currentAppState].StepColor;
                    }
                    else
                    {
                        feedbackBox.gameObject.SetActive(true);
                        feedbackBox.text = stateParams[_currentAppState].StepMessage;
                        feedbackBox.color = Color.red;
                    }
                }
            }
        }

        /// <summary>
        /// Start is called on the frame when a script is enabled just before any
        /// of the Update methods are called the first time.
        /// </summary>
        public override void Start()
        {
            Debug.Log(">>Azure Spatial Anchors Demo Script Start");
            isCreateMode = PlayerPrefs.GetInt(MenuUIController.IS_CREATE_MODE_PP_KEY, 0) == 1;
            _firebaseLoader = GetComponent<FirebaseLoader>();

            base.Start();

            if (!SanityCheckAccessConfiguration())
                return;

            feedbackBox.gameObject.SetActive(isCreateMode);
            swipeUpText.gameObject.SetActive(!isCreateMode);
            TogglePlaneVisualizer(isCreateMode);
            swipeRightText.transform.position = isCreateMode ? swipeUpText.transform.position : swipeRightText.transform.position;
            swipeRightText.CrossFadeAlpha(0f, 10f, false);
            saveButton.gameObject.SetActive(false);
            _firebaseLoader.ToggleLocationDataUI(false);
            if (!isCreateMode)
                swipeUpText.CrossFadeAlpha(0f, 10f, false);

            StopCoroutine("StartDemoCoroutine");
            StartCoroutine(StartDemoCoroutine(true));
            Debug.Log("Azure Spatial Anchors Demo script started");
        }

        private IEnumerator StartDemoCoroutine(bool awaitFrame)
        {
            if (awaitFrame)
                yield return 0;

            isAdvancingDemo = true;
            yield return (advanceDemoTask = AdvanceDemoAsync());
        }

        public void OnSaveButtonPressed()
        {
            if (advanceDemoTask.Status != TaskStatus.RanToCompletion || isAdvancingDemo)
                return;

            if (currentAppState != AppState.DemoStepComplete)
            {
                StopCoroutine("StartDemoCoroutine");
                isSavePressed = true;
                StartCoroutine(StartDemoCoroutine(false));
            }
            else
            {
                ReturnToLauncher();
            }
        }

        protected override void OnCloudAnchorLocated(AnchorLocatedEventArgs args)
        {
            base.OnCloudAnchorLocated(args);

            if (args.Status == LocateAnchorStatus.Located)
            {
                CloudSpatialAnchor cloudAnchor = args.Anchor;
                currentCloudAnchor = args.Anchor;

                UnityDispatcher.InvokeOnAppThread(() =>
                {
                    currentCloudAnchor = cloudAnchor;
                    _firebaseLoader.LoadOrMoveUserModel(cloudAnchor);
                    Debug.Log(cloudAnchor.Identifier + " located with strategy: " + args.Strategy.ToString());
                });
            }
        }

        public void OnApplicationFocus(bool focusStatus)
        {
#if UNITY_ANDROID
            // We may get additional permissions at runtime. Enable the sensors once app is resumed
            if (focusStatus && SensorProviderHelper.locationProvider != null)
            {
                ConfigureSensors();
            }
#endif
        }

        /// <summary>
        /// Update is called every frame, if the MonoBehaviour is enabled.
        /// </summary>
        public override void Update()
        {
            base.Update();

            if (spawnedObjectMat != null)
            {
                float rat = 0.1f;
                float createProgress = 0f;
                if (CloudManager.SessionStatus != null)
                {
                    createProgress = CloudManager.SessionStatus.RecommendedForCreateProgress;
                }
                rat += (Mathf.Min(createProgress, 1) * 0.9f);
                spawnedObjectMat.color = GetStepColor() * rat;
            }
        }

        protected override bool IsPlacingObject()
        {
            return currentAppState == AppState.DemoStepCreateLocalAnchor || currentAppState == AppState.DemoStepSaveCloudAnchor;
        }

        protected override void OnSelectObjectInteraction(Vector3 hitPoint, object target)
        {
            if (IsPlacingObject())
            {
                Quaternion rotation = Quaternion.AngleAxis(0, Vector3.up);
                SpawnOrMoveCurrentAnchoredObject(hitPoint, rotation);

                saveButton.gameObject.SetActive(true);
            }
        }

        protected override Color GetStepColor()
        {
            return stateParams[currentAppState].StepColor;
        }

        protected override async Task OnSaveCloudAnchorSuccessfulAsync()
        {
            await base.OnSaveCloudAnchorSuccessfulAsync();

            Debug.Log("Anchor created, yay!");

            string anchorId = currentCloudAnchor.Identifier;

            // Sanity check that the object is still where we expect
            Pose anchorPose = Pose.identity;

#if UNITY_ANDROID || UNITY_IOS
            anchorPose = currentCloudAnchor.GetPose();
#endif
            // HoloLens: The position will be set based on the unityARUserAnchor that was located.
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                SpawnOrMoveCurrentAnchoredObject(anchorPose.position, anchorPose.rotation);

                // Write the anchor id and location info to Firebase;
                _firebaseLoader.AnchorIdToWrite = anchorId;
                _firebaseLoader.ToggleLocationDataUI(true);
                feedbackBox.gameObject.SetActive(false);
                saveButton.gameObject.SetActive(false);
            });
        }

        protected override void OnSaveCloudAnchorFailed(Exception exception)
        {
            base.OnSaveCloudAnchorFailed(exception);
            Debug.LogError(exception);
            ReturnToLauncher();
        }

        public void OnAnchorCreationCompleted()
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                FindObjectOfType<IapManager>().ConsumeTokens(1);
                _firebaseLoader.SplashText("Location Created:  1 Token Used", Color.green);

                StopCoroutine("StartDemoCoroutine");
                currentAppState = AppState.DemoStepComplete;

                saveButton.GetComponentInChildren<Text>().text = "DONE";
                // saveButton.GetComponent<Image>().color = Color.green;
                saveButton.gameObject.SetActive(true);
            });
        }

        protected override void OnSwipeUp()
        {
            if (!isCreateMode && !isSwipeCooldown)
            {
                SetBypassCache(true);

                if (advanceDemoTask.Status == TaskStatus.RanToCompletion && !isAdvancingDemo)
                {
                    _firebaseLoader.SplashText("Refreshing Model Locations", Color.white);
                    currentAppState = AppState.DemoStepResetSession;
                    StopCoroutine("StartDemoCoroutine");
                    StartCoroutine(StartDemoCoroutine(false));

                    StopCoroutine(SwipeCooldown());
                    StartCoroutine(SwipeCooldown());
                }
            }
        }

        private bool isSwipeCooldown;

        private IEnumerator SwipeCooldown()
        {
            isSwipeCooldown = true;
            yield return new WaitForSeconds(5f);
            isSwipeCooldown = false;
        }

        protected override void OnSwipeRight()
        {
            ReturnToLauncher();
        }

        public async override Task AdvanceDemoAsync()
        {
            while (isAdvancingDemo)
            {
                switch (currentAppState)
                {
                    case AppState.FirebaseInit:
                        if (_firebaseLoader.IsReady)
                            currentAppState = AppState.DemoStepCreateSession;
                        break;
                    case AppState.DemoStepCreateSession:
                        if (CloudManager.Session == null)
                        {
                            await CloudManager.CreateSessionAsync();
                        }
                        currentCloudAnchor = null;
                        currentAppState = AppState.DemoStepConfigSession;
                        break;
                    case AppState.DemoStepConfigSession:
                        ConfigureSession();
                        currentAppState = AppState.DemoStepStartSession;
                        break;
                    case AppState.DemoStepStartSession:
                        await CloudManager.StartSessionAsync();
                        currentAppState = AppState.DemoStepCreateLocationProvider;
                        break;
                    //*Coarse Reloc*********
                    case AppState.DemoStepCreateLocationProvider:
                        SensorProviderHelper.locationProvider = new PlatformLocationProvider();
                        CloudManager.Session.LocationProvider = SensorProviderHelper.locationProvider;
                        currentAppState = AppState.DemoStepConfigureSensors;
                        break;
                    case AppState.DemoStepConfigureSensors:
                        SensorPermissionHelper.RequestSensorPermissions();
                        ConfigureSensors();
                        currentAppState = AppState.DemoStepCheckAdminMode;
                        break;
                    case AppState.DemoStepCheckAdminMode:
                        if (isCreateMode)
                        {
                            enableAdvancingOnSelect = true;
                            spawnedObject = null;
                            currentAppState = AppState.DemoStepCreateLocalAnchor;
                        }
                        else
                        {
                            currentAppState = AppState.DemoStepCreateSessionForQuery;
                        }
                        break;
                    /*******************************************************************
                    // Will enter these steps if Admin 
                    *******************************************************************/
                    case AppState.DemoStepCreateLocalAnchor:
                        if (spawnedObject != null && isSavePressed)
                        {
                            currentAppState = AppState.DemoStepSaveCloudAnchor;
                        }
                        else
                        {
                            // Object interaction + Save button will advance instead
                            isAdvancingDemo = false;
                        }
                        break;
                    case AppState.DemoStepSaveCloudAnchor:
                        // Will restart at admin check if save successful
                        saveButton.gameObject.SetActive(false);
                        currentAppState = AppState.DemoStepSavingCloudAnchor;
                        await SaveCurrentObjectAnchorToCloudAsync();
                        isSavePressed = false;
                        isAdvancingDemo = false;
                        break;
                    /*******************************************************************
                    // End Admin steps
                    *******************************************************************/
                    case AppState.DemoStepResetSession:
                        await CloudManager.ResetSessionAsync();
                        currentAppState = AppState.DemoStepCreateSessionForQuery;
                        break;
                    //************************************************************************
                    case AppState.DemoStepCreateSessionForQuery:
                        //await _firebaseLoader.FetchUserLocationAnchorIdsAsync();
                        ConfigureSession();
                        SensorProviderHelper.locationProvider = new PlatformLocationProvider();
                        CloudManager.Session.LocationProvider = SensorProviderHelper.locationProvider;
                        ConfigureSensors();
                        currentAppState = AppState.DemoStepStartSessionForQuery;
                        break;
                    case AppState.DemoStepStartSessionForQuery:
                        if (!CloudManager.IsSessionStarted)
                            await CloudManager.StartSessionAsync();
                        currentAppState = AppState.DemoStepLookForAnchorsNearDevice;
                        break;
                    case AppState.DemoStepLookForAnchorsNearDevice:
                        currentWatcher = CreateWatcher();
                        currentAppState = AppState.DemoStepLookingForAnchorsNearDevice;
                        break;
                    case AppState.DemoStepLookingForAnchorsNearDevice:
                        isAdvancingDemo = false;
                        break;
                    //************************************************************************
                    case AppState.DemoStepStopWatcher:
                        if (currentWatcher != null)
                        {
                            currentWatcher.Stop();
                            currentWatcher = null;
                        }
                        currentAppState = AppState.DemoStepStopSessionForQuery;
                        break;
                    case AppState.DemoStepStopSessionForQuery:
                        if (CloudManager.IsSessionStarted)
                            CloudManager.StopSession();
                        currentWatcher = null;
                        currentAppState = AppState.DemoStepComplete;
                        break;
                    case AppState.DemoStepComplete:
                        currentCloudAnchor = null;
                        //currentAppState = AppState.DemoStepCreateSession;
                        CleanupSpawnedAnchorObjects();
                        break;
                    default:
                        Debug.Log("Shouldn't get here for app state " + currentAppState.ToString());
                        break;
                }
            }
        }

        //*Coarse Reloc**************************************
        public async override Task EnumerateAllNearbyAnchorsAsync()
        {
            Debug.Log("Enumerating near-device spatial anchors in the cloud");

            NearDeviceCriteria criteria = new NearDeviceCriteria();
            criteria.DistanceInMeters = 3;
            criteria.MaxResultCount = 5;

            var cloudAnchorSession = CloudManager.Session;

            var spatialAnchorIds = await cloudAnchorSession.GetNearbyAnchorIdsAsync(criteria);

            Debug.LogFormat("Got ids for {0} anchors", spatialAnchorIds.Count);

            List<CloudSpatialAnchor> spatialAnchors = new List<CloudSpatialAnchor>();

            foreach (string anchorId in spatialAnchorIds)
            {
                var anchor = await cloudAnchorSession.GetAnchorPropertiesAsync(anchorId);
                Debug.LogFormat("Received information about spatial anchor {0}", anchor.Identifier);
                spatialAnchors.Add(anchor);
            }

            //feedbackBox.text = $"Found {spatialAnchors.Count} anchors nearby";
        }

        protected override void CleanupSpawnedAnchorObjects()
        {
            _firebaseLoader.CleanupSpawnedModels();
        }

        private void ConfigureSession()
        {
            HashSet<string> anchorsToFind = new HashSet<string>();

            if (currentAppState == AppState.DemoStepCreateSessionForQuery)
            {
                foreach (string id in _firebaseLoader.QuerriedAnchorIds)
                {
                    anchorsToFind.Add(id);
                }
            }

            SetAnchorIdsToLocate(anchorsToFind);
        }

        //*Coarse Reloc*
        private void ConfigureSensors()
        {
            SensorProviderHelper.locationProvider.Sensors.GeoLocationEnabled = SensorPermissionHelper.HasGeoLocationPermission();

            SensorProviderHelper.locationProvider.Sensors.WifiEnabled = SensorPermissionHelper.HasWifiPermission();

            SensorProviderHelper.locationProvider.Sensors.BluetoothEnabled = SensorPermissionHelper.HasBluetoothPermission();
            SensorProviderHelper.locationProvider.Sensors.KnownBeaconProximityUuids = CoarseRelocSettings.KnownBluetoothProximityUuids;
        }
        //*************

        private void TogglePlaneVisualizer(bool isActive)
        {
            GameObject planeVisualizerPrefab = FindObjectOfType<XRCameraPicker>().ARFoundationCameraTree.GetComponent<ARPlaneManager>().planePrefab;
            planeVisualizerPrefab.GetComponent<ARPlaneMeshVisualizer>().enabled = isActive;
            planeVisualizerPrefab.GetComponent<LineRenderer>().enabled = isActive;
            planeVisualizerPrefab.GetComponent<MeshRenderer>().enabled = isActive;
        }
    }
}
