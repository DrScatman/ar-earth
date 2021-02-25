// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    [RequireComponent(typeof(FirebaseLoader))]
    public class AzureSpatialAnchorsBasicDemoScript : DemoScriptBase
    {
        [SerializeField] private bool debug;
        [SerializeField] private bool useCoarseRelocalization;
        [SerializeField] private Text swipeText;

        private string currentAnchorId = "";
        private List<GameObject> allDiscoveredAnchors;
        private FirebaseLoader _firebaseLoader;
        private Button nextButton;

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
            DemoStepStopSession,
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
            { AppState.DemoStepCheckAdminMode,new DemoStepParams() { StepMessage = "Checking for admin mode", StepColor = Color.clear }},
            { AppState.DemoStepCreateLocalAnchor,new DemoStepParams() { StepMessage = "Tap a surface to add the Local Anchor.", StepColor = Color.blue }},
            { AppState.DemoStepSaveCloudAnchor,new DemoStepParams() { StepMessage = "Next: Save Local Anchor to cloud", StepColor = Color.yellow }},
            { AppState.DemoStepSavingCloudAnchor,new DemoStepParams() { StepMessage = "Saving local Anchor to cloud...", StepColor = Color.yellow }},
            { AppState.DemoStepStopSession,new DemoStepParams() { StepMessage = "Next: Stop Azure Spatial Anchors Session", StepColor = Color.green }},
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
            _firebaseLoader = GetComponent<FirebaseLoader>();

            base.Start();

            if (!SanityCheckAccessConfiguration())
                return;

            foreach (var button in XRUXPicker.Instance.GetDemoButtons())
            {
                if (button.gameObject.name == "Next Button")
                    nextButton = button;

                button.gameObject.SetActive(false);
            }
            feedbackBox.gameObject.SetActive(debug);

            swipeText.CrossFadeAlpha(0f, 7f, false);

            StartCoroutine(StartDemoCoroutine());
            Debug.Log("Azure Spatial Anchors Demo script started");
        }

        private IEnumerator StartDemoCoroutine()
        {
            //returning 0 will make it wait 1 frame
            yield return 0;
            yield return AdvanceDemoAsync();
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
                    Pose anchorPose = Pose.identity;

#if UNITY_ANDROID || UNITY_IOS
                    anchorPose = cloudAnchor.GetPose();
#endif
                    // HoloLens: The position will be set based on the unityARUserAnchor that was located.
                    GameObject newSpawnedObject = SpawnNewAnchoredObject(anchorPose.position, anchorPose.rotation, cloudAnchor);
                    allDiscoveredAnchors.Add(newSpawnedObject);
                    base.spawnedObject = newSpawnedObject;

                    _firebaseLoader.LoadOrMoveUserModel(newSpawnedObject.transform.position, cloudAnchor.Identifier);
                    newSpawnedObject.transform.GetChild(0).gameObject.SetActive(true);
                });
            }
        }

        public void OnModelLoadCompleted()
        {
            foreach (GameObject anchor in allDiscoveredAnchors)
            {
                anchor.GetComponent<MeshRenderer>().enabled = false;
                anchor.transform.GetChild(0).gameObject.SetActive(false);
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
            return currentAppState == AppState.DemoStepCreateLocalAnchor;
        }

        protected override Color GetStepColor()
        {
            return stateParams[currentAppState].StepColor;
        }

        protected override async Task OnSaveCloudAnchorSuccessfulAsync()
        {
            await base.OnSaveCloudAnchorSuccessfulAsync();

            Debug.Log("Anchor created, yay!");

            currentAnchorId = currentCloudAnchor.Identifier;

            // Sanity check that the object is still where we expect
            Pose anchorPose = Pose.identity;

#if UNITY_ANDROID || UNITY_IOS
            anchorPose = currentCloudAnchor.GetPose();
#endif
            // HoloLens: The position will be set based on the unityARUserAnchor that was located.

            SpawnOrMoveCurrentAnchoredObject(anchorPose.position, anchorPose.rotation);

            // Write the anchor id and location info to Firebase;
            _firebaseLoader.AnchorIdToWrite = currentAnchorId;
            _firebaseLoader.ToggleLocationDataUI(true);

            currentAppState = AppState.DemoStepStopSession;
        }

        protected override void OnSaveCloudAnchorFailed(Exception exception)
        {
            base.OnSaveCloudAnchorFailed(exception);

            currentAnchorId = string.Empty;
        }

        public async override Task AdvanceDemoAsync()
        {
            while (canAdvanceDemo)
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
                        currentAnchorId = "";
                        currentCloudAnchor = null;
                        currentAppState = AppState.DemoStepConfigSession;
                        break;
                    case AppState.DemoStepConfigSession:
                        ConfigureSession();
                        currentAppState = AppState.DemoStepStartSession;
                        break;
                    case AppState.DemoStepStartSession:
                        await CloudManager.StartSessionAsync();
                        if (useCoarseRelocalization)
                            currentAppState = AppState.DemoStepCreateLocationProvider;
                        else
                            currentAppState = AppState.DemoStepCheckAdminMode;
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
                        if (_firebaseLoader.SelectedUserId == MenuManager.ADMIN_USER_ID)
                        {
                            currentAppState = AppState.DemoStepCreateLocalAnchor;
                            enableAdvancingOnSelect = true;

                            UnityDispatcher.InvokeOnAppThread(() =>
                            {
                                feedbackBox.gameObject.SetActive(true);
                                feedbackBox.text = stateParams[_currentAppState].StepMessage;
                                feedbackBox.color = Color.green;
                            });
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
                        if (spawnedObject != null)
                        {
                            currentAppState = AppState.DemoStepSaveCloudAnchor;
                        }
                        UnityDispatcher.InvokeOnAppThread(() => nextButton.gameObject.SetActive(true));
                        canAdvanceDemo = false;
                        break;
                    case AppState.DemoStepSaveCloudAnchor:
                        nextButton.gameObject.SetActive(false);
                        currentAppState = AppState.DemoStepSavingCloudAnchor;
                        // Will advance if save successful
                        await SaveCurrentObjectAnchorToCloudAsync();
                        break;
                    case AppState.DemoStepStopSession:
                        CloudManager.StopSession();
                        CleanupSpawnedAnchorObjects();
                        await CloudManager.ResetSessionAsync();
                        currentAppState = AppState.DemoStepCreateSessionForQuery;

                        feedbackBox.text = _firebaseLoader.Status;
                        canAdvanceDemo = false;
                        break;
                    /*******************************************************************
                    // End Admin steps
                    *******************************************************************/
                    case AppState.DemoStepCreateSessionForQuery:
                        await _firebaseLoader.FetchUserLocationAnchorIdsAsync();
                        ConfigureSession();
                        if (useCoarseRelocalization)
                        {
                            SensorProviderHelper.locationProvider = new PlatformLocationProvider();
                            CloudManager.Session.LocationProvider = SensorProviderHelper.locationProvider;
                            ConfigureSensors();
                        }
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
                        canAdvanceDemo = false;
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
                        CloudManager.StopSession();
                        currentWatcher = null;
                        currentAppState = AppState.DemoStepComplete;
                        break;
                    case AppState.DemoStepComplete:
                        currentCloudAnchor = null;
                        currentAppState = AppState.DemoStepCreateSession;
                        CleanupSpawnedAnchorObjects();
                        _firebaseLoader.CleanupSpawnedModels();
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
            criteria.DistanceInMeters = 5;
            criteria.MaxResultCount = 20;

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
            base.CleanupSpawnedAnchorObjects();

            foreach (GameObject anchor in allDiscoveredAnchors)
            {
                Destroy(anchor);
            }
            allDiscoveredAnchors.Clear();

            _firebaseLoader.CleanupSpawnedModels();
        }

        private void ConfigureSession()
        {
            HashSet<string> anchorsToFind = new HashSet<string>();

            if (currentAppState == AppState.DemoStepCreateSessionForQuery)
            {
                // if (currentAnchorId != null && currentAnchorId != string.Empty)
                //     anchorsToFind.Add(currentAnchorId);

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
    }
}
