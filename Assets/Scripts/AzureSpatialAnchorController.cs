// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    [RequireComponent(typeof(FirebaseLoader))]
    public class AzureSpatialAnchorController : DemoScriptBase
    {
        [SerializeField] private Button saveButton;
        [SerializeField] private Button enumerateButton;

        public Button createLocationButton;
        public Button addModelButton;

        private Text addModelText;
        private bool isCreateMode;
        private bool isSavePressed;
        private string anchorIdToDelete;
        private static readonly string FIRST_BOOT_PP_KEY = "FIRST_BOOT";

        internal enum AppState
        {
            FirebaseInit = 0,
            CreateSession,
            ConfigSession,
            StartSession,
            CreateLocationProvider,
            ConfigureSensors,
            CheckAdminMode,
            DeleteOldAnchor,
            CreateLocalAnchor,
            SaveCloudAnchor,
            SavingCloudAnchor,
            ResetSession,
            DestroySession,
            CreateSessionForQuery,
            StartSessionForQuery,
            LookForAnchorsNearDevice,
            LookingForAnchorsNearDevice,
            StopWatcher,
            //******************************
            StopSessionForQuery,
            Complete
        }

        private readonly Dictionary<AppState, DemoStepParams> stateParams = new Dictionary<AppState, DemoStepParams>
        {
            { AppState.FirebaseInit,new DemoStepParams() { StepMessage = "Connecting to Firebase database...", StepColor = Color.clear }},
            { AppState.CreateSession,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            { AppState.ConfigSession,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            { AppState.StartSession,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            //*Added from Coarse Reloc******
            { AppState.CreateLocationProvider,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            { AppState.ConfigureSensors,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            //******************************
            { AppState.CheckAdminMode,new DemoStepParams() { StepMessage = "", StepColor = Color.white }},
            { AppState.CreateLocalAnchor,new DemoStepParams() { StepMessage = "Point at a surface. Then tap to add a 3D model location.", StepColor = Color.white }},
            { AppState.SaveCloudAnchor,new DemoStepParams() { StepMessage = "Press button when ready to save the 3D-model location", StepColor = Color.blue }},
            { AppState.SavingCloudAnchor,new DemoStepParams() { StepMessage = "Saving location to cloud...", StepColor = Color.yellow }},
            { AppState.ResetSession,new DemoStepParams() { StepMessage = "Resetting session", StepColor = Color.white }},
            { AppState.CreateSessionForQuery,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            { AppState.StartSessionForQuery,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            //Commenting out to test Coarse Reloc
            //{ AppState.LookForAnchor,new DemoStepParams() { StepMessage = "Next: Look for Anchor", StepColor = Color.clear }},
            //{ AppState.LookingForAnchor,new DemoStepParams() { StepMessage = "Looking for Anchor...", StepColor = Color.clear }},
            { AppState.DeleteOldAnchor,new DemoStepParams() { StepMessage = "", StepColor = Color.yellow }},
            //*Added from Coarse Reloc******
            { AppState.LookForAnchorsNearDevice,new DemoStepParams() { StepMessage = "Next: Look for Anchors near device", StepColor = Color.clear }},
            { AppState.LookingForAnchorsNearDevice,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
            { AppState.StopWatcher,new DemoStepParams() { StepMessage = "Next: Stop Watcher", StepColor = Color.yellow }},
            //******************************
            { AppState.StopSessionForQuery,new DemoStepParams() { StepMessage = "Next: Stop Azure Spatial Anchors Session for query", StepColor = Color.grey }},
            { AppState.Complete,new DemoStepParams() { StepMessage = "", StepColor = Color.clear }},
        };

        private AppState _currentAppState = AppState.FirebaseInit;

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
            if (firebaseLoader == null)
                firebaseLoader = GetComponent<FirebaseLoader>();

            base.Start();

            if (!SanityCheckAccessConfiguration())
                return;

            if (!PlayerPrefs.HasKey(FIRST_BOOT_PP_KEY))
            {
                PlayerPrefs.SetInt(FIRST_BOOT_PP_KEY, 1);
                firebaseLoader.SplashText("Please Enable App Permissions!", Color.white);
            }

            feedbackBox.gameObject.SetActive(isCreateMode);
            TogglePlaneVisualizer(isCreateMode);
            saveButton.gameObject.SetActive(false);
            enumerateButton.gameObject.SetActive(!isCreateMode);
            firebaseLoader.ToggleLocationDataUI(false);
            anchorIdToDelete = PlayerPrefs.GetString(FirebaseManager.ANCHOR_ID_TO_DELETE_PP_KEY, "");

            StartDemo();
            Debug.Log("Azure Spatial Anchors Demo script started");
        }

        private void OnARSessionTracking(ARSessionStateChangedEventArgs args)
        {
            if (args.state == ARSessionState.SessionTracking && !hasStarted)
            {
                hasStarted = true;
                ARSession.stateChanged -= OnARSessionTracking;
                UnityDispatcher.InvokeOnAppThread(() => StartDemo());
            }
        }

        private bool hasStarted;

        private async void StartDemo()
        {
            if (ARSession.state != ARSessionState.SessionTracking && !hasStarted)
            {
                Debug.Log("Awaiting AR Session Tracking...");
                ARSession.stateChanged += OnARSessionTracking;
                return;
            }

            try
            {
                Debug.Log("Starting Demo");
                isAdvancingDemo = true;

                advanceDemoTask = AdvanceDemoAsync();
                await advanceDemoTask;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public void OnSaveButtonPressed()
        {
            if (currentAppState != AppState.Complete)
            {
                isSavePressed = true;

                if (!isAdvancingDemo)
                    StartDemo();
            }
            else
            {
                OnSceneChangeButton("SetModel UI");
            }
        }

        protected override void OnCloudAnchorLocated(AnchorLocatedEventArgs args)
        {
            base.OnCloudAnchorLocated(args);

            if (args.Status == LocateAnchorStatus.Located)
            {
                if (args.Anchor.Identifier == anchorIdToDelete)
                {
                    DeleteCloudAnchor(null, args.Anchor);
                    return;
                }

                CloudSpatialAnchor cloudAnchor = args.Anchor;
                currentCloudAnchor = args.Anchor;

                UnityDispatcher.InvokeOnAppThread(() =>
                {
                    currentCloudAnchor = cloudAnchor;
                    firebaseLoader.LoadOrMoveUserModel(currentCloudAnchor);

                    if (!hasNotifiedChangeModel)
                    {
                        hasNotifiedChangeModel = true;
                        addModelText.text = $"Change{Environment.NewLine}3D Model";
                        addModelText.color = new Color32(0, 255, 255, 200);
                        addModelText.CrossFadeAlpha(0f, 20f, true);
                    }
                });

                Debug.Log("Located with strategy: " + args.Strategy);
            }
        }

        private static bool hasNotifiedChangeModel;

        public void OnMaxModelsLocated()
        {
            Debug.Log("All 3D Models Located!");
            firebaseLoader.SplashText("All Your 3D Models Were Located!", Color.white);
            // StopDemo();
        }

        private async void DeleteCloudAnchor(string anchorId, CloudSpatialAnchor cloudAnchor = null)
        {
            if ((!string.IsNullOrEmpty(anchorId) || cloudAnchor != null)
                && CloudManager != null && CloudManager.Session != null)
            {
                try
                {
                    if (cloudAnchor == null)
                        cloudAnchor = await CloudManager.Session.GetAnchorPropertiesAsync(anchorId);

                    if (cloudAnchor != null)
                        await CloudManager.DeleteAnchorAsync(cloudAnchor);

                    PlayerPrefs.DeleteKey(FirebaseManager.ANCHOR_ID_TO_DELETE_PP_KEY);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    PlayerPrefs.DeleteKey(FirebaseManager.ANCHOR_ID_TO_DELETE_PP_KEY);
                }
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

            if (Application.platform == RuntimePlatform.Android)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    OnBackButton();
                }
            }
        }

        protected override bool IsPlacingObject()
        {
            return currentAppState == AppState.CreateLocalAnchor && !isSavePressed;
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
                firebaseLoader.AnchorIdToWrite = anchorId;
                firebaseLoader.ToggleLocationDataUI(true);
                feedbackBox.gameObject.SetActive(false);
                saveButton.gameObject.SetActive(false);
            });
        }

        protected override void OnSaveCloudAnchorFailed(Exception exception)
        {
            base.OnSaveCloudAnchorFailed(exception);
        }

        public void OnAnchorCreationCompleted()
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                try
                {
                    isAdvancingDemo = false;
                    currentAppState = AppState.Complete;

                    IapManager.Instance.ConsumeTokens(1);
                    firebaseLoader.SplashText("Location Created Successfully!", Color.green);

                    OnSceneChangeButton("SetModel UI");
                }
                catch (Exception e)
                {
                    firebaseLoader.SplashText(e.Message, Color.red);
                }
            });
        }

        public void OnBackButton()
        {
            if (isCreateMode && (firebaseLoader.HasAnchorLocations || IapManager.Instance.HasUsedFreeLocations()))
            {
                ToggleCreateMode(false);
            }
            else
            {
                OnSceneChangeButton("Menu UI");
            }
        }

        public void OnSwipeUp()
        {
            // if (!isCooldown && !firebaseLoader.IsSpawnedObjectSelected())
            // {
            //     SetBypassCache(true);

            //     if (advanceDemoTask.Status == TaskStatus.RanToCompletion && !isAdvancingDemo)
            //     {
            //         firebaseLoader.SplashText("Refreshing...", Color.white);
            //         firebaseLoader.RemoveLoadingModels();
            //         currentAppState = AppState.ResetSession;
            //         if (!isAdvancingDemo)
            //             StartDemo();

            //         StopCoroutine("CooldownCoroutine");
            //         StartCoroutine(CooldownCoroutine(5f));
            //     }
            // }
        }

        private bool isCooldown;

        private IEnumerator CooldownCoroutine(float seconds)
        {
            isCooldown = true;
            yield return new WaitForSeconds(seconds);
            isCooldown = false;
        }

        private void SetButtons()
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                enumerateButton.gameObject.SetActive(!isCreateMode);
                createLocationButton.gameObject.SetActive(firebaseLoader.IsReady);
                addModelButton.gameObject.SetActive(firebaseLoader.HasAnchorLocations && !isCreateMode);

                if (createLocationButton.gameObject.activeSelf && !isCreateMode)
                    createLocationButton.GetComponentInChildren<Text>().CrossFadeAlpha(0f, 20f, true);
                if (addModelButton.gameObject.activeSelf)
                {
                    addModelText = addModelButton.GetComponentInChildren<Text>();
                    addModelText.text = $"Add{Environment.NewLine}3D Model";
                    addModelText.color = new Color32(255, 255, 255, 170);
                    addModelText.CrossFadeAlpha(0f, 20f, true);
                }

                if (isCreateMode)
                    firebaseLoader.SplashText("Add a 3D Model Location Anywhere, For Anyone To See!", Color.white);
                else
                    firebaseLoader.SplashText("Scan Surroundings To Find 3D Models", Color.white);
            });
        }

        public async override Task AdvanceDemoAsync()
        {
            while (isAdvancingDemo)
            {
                switch (currentAppState)
                {
                    case AppState.FirebaseInit:
                        if (!firebaseLoader.IsReady)
                        {
                            await Task.Delay(330);
                        }
                        else
                        {
                            if (!firebaseLoader.HasAnchorLocations && !isCreateMode && !IapManager.Instance.HasUsedFreeLocations())
                            {
                                ToggleCreateMode(true);
                                return;
                            }
                            SetButtons();
                            currentAppState = AppState.CreateSession;
                        }
                        break;
                    case AppState.CreateSession:
                        if (CloudManager.Session == null)
                        {
                            await CloudManager.CreateSessionAsync();
                        }
                        currentCloudAnchor = null;
                        currentAppState = AppState.CheckAdminMode;
                        break;
                    case AppState.CheckAdminMode:
                        if (isCreateMode)
                        {
                            enableAdvancingOnSelect = true;
                            spawnedObject = null;
                            currentAppState = AppState.ConfigSession;
                        }
                        else
                        {
                            currentAppState = AppState.CreateSessionForQuery;
                        }
                        break;
                    /*******************************************************************
                    // Will enter these steps if Admin 
                    *******************************************************************/
                    case AppState.ConfigSession:
                        ConfigureSession();
                        currentAppState = AppState.StartSession;
                        break;
                    case AppState.StartSession:
                        if (!CloudManager.IsSessionStarted)
                            await CloudManager.StartSessionAsync();
                        currentAppState = AppState.CreateLocationProvider;
                        break;
                    //*Coarse Reloc*********
                    case AppState.CreateLocationProvider:
                        if (SensorProviderHelper.locationProvider == null)
                            SensorProviderHelper.locationProvider = new PlatformLocationProvider();
                        CloudManager.Session.LocationProvider = SensorProviderHelper.locationProvider;
                        currentAppState = AppState.ConfigureSensors;
                        break;
                    case AppState.ConfigureSensors:
                        SensorPermissionHelper.RequestSensorPermissions();
                        ConfigureSensors();
                        currentAppState = AppState.DeleteOldAnchor;
                        break;
                    case AppState.DeleteOldAnchor:
                        DeleteCloudAnchor(anchorIdToDelete);
                        currentAppState = AppState.CreateLocalAnchor;
                        break;
                    case AppState.CreateLocalAnchor:
                        if (spawnedObject == null || !isSavePressed)
                            await Task.Delay(330);
                        else
                            currentAppState = AppState.SaveCloudAnchor;
                        break;
                    case AppState.SaveCloudAnchor:
                        UnityDispatcher.InvokeOnAppThread(() => saveButton.gameObject.SetActive(false));
                        currentAppState = AppState.SavingCloudAnchor;
                        await SaveCurrentObjectAnchorToCloudAsync(firebaseLoader.UserId);
                        isSavePressed = false;
                        isAdvancingDemo = false;
                        break;
                    /*******************************************************************
                    // End Admin steps
                    *******************************************************************/
                    case AppState.ResetSession:
                        await CloudManager.ResetSessionAsync();
                        if (isCreateMode)
                            currentAppState = AppState.ConfigSession;
                        else
                            currentAppState = AppState.CreateSessionForQuery;
                        break;
                    //************************************************************************
                    case AppState.CreateSessionForQuery:
                        // await _firebaseLoader.FetchLocationIdsAsync();
                        ConfigureSession();
                        if (SensorProviderHelper.locationProvider == null)
                            SensorProviderHelper.locationProvider = new PlatformLocationProvider();
                        CloudManager.Session.LocationProvider = SensorProviderHelper.locationProvider;
                        SensorPermissionHelper.RequestSensorPermissions();
                        ConfigureSensors();
                        currentAppState = AppState.StartSessionForQuery;
                        break;
                    case AppState.StartSessionForQuery:
                        if (!CloudManager.IsSessionStarted)
                        {
                            try
                            {
                                await CloudManager.StartSessionAsync();
                            }
                            catch (Exception e)
                            {
                                Debug.LogWarning("Error Starting Cloud Manager Session...");
                                Debug.LogError(e);
                            }
                        }
                        currentAppState = AppState.LookForAnchorsNearDevice;
                        break;
                    case AppState.LookForAnchorsNearDevice:
                        currentWatcher = CreateWatcher();
                        currentAppState = AppState.LookingForAnchorsNearDevice;
                        break;
                    case AppState.LookingForAnchorsNearDevice:
                        isAdvancingDemo = false;
                        break;
                    //************************************************************************
                    case AppState.StopWatcher:
                        if (currentWatcher != null)
                        {
                            currentWatcher.Stop();
                            currentWatcher = null;
                        }
                        currentAppState = AppState.StopSessionForQuery;
                        break;
                    case AppState.StopSessionForQuery:
                        if (CloudManager.IsSessionStarted)
                            CloudManager.StopSession();
                        currentWatcher = null;
                        SensorProviderHelper.locationProvider = null;
                        currentAppState = AppState.Complete;
                        break;
                    case AppState.Complete:
                        currentCloudAnchor = null;
                        CleanupSpawnedAnchorObjects();
                        spawnedObject = null;
                        isAdvancingDemo = false;
                        break;
                    default:
                        Debug.Log("Shouldn't get here for app state " + currentAppState.ToString());
                        break;
                }
            }
        }

        private bool isEnumerating;
        //*Coarse Reloc**************************************
        public async override Task EnumerateAllNearbyAnchorsAsync(float distanceInMeters)
        {
            firebaseLoader.SplashText($"Locations Within {distanceInMeters}m {Environment.NewLine}Searching...", Color.white);

            if (!isEnumerating)
            {
                if (CloudManager != null & CloudManager.Session != null)
                {
                    isEnumerating = true;

                    NearDeviceCriteria criteria = new NearDeviceCriteria();
                    criteria.DistanceInMeters = distanceInMeters;
                    criteria.MaxResultCount = 20;

                    var cloudAnchorSession = CloudManager.Session;

                    try
                    {
                        var spatialAnchorIds = await cloudAnchorSession.GetNearbyAnchorIdsAsync(criteria);

                        string locationsText = "";

                        foreach (string id in spatialAnchorIds)
                        {
                            if (firebaseLoader.FetchedLocationsDict.ContainsKey(id))
                            {
                                if (!string.IsNullOrEmpty(locationsText))
                                    locationsText += Environment.NewLine;

                                var payload = firebaseLoader.FetchedLocationsDict[id];

                                if (payload.HasModel && payload.ModelPayload != null)
                                    locationsText += $"{firebaseLoader.FetchedLocationsDict[id].ModelPayload.ToString()}  @  {firebaseLoader.FetchedLocationsDict[id].ToString()}";
                                else
                                    locationsText += $"NONE  @  {firebaseLoader.FetchedLocationsDict[id].ToString()}";
                            }
                        }

                        if (!string.IsNullOrEmpty(locationsText))
                            firebaseLoader.SplashText(locationsText, Color.white);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                        firebaseLoader.SplashText(e.Message, Color.red);
                    }
                }
                isEnumerating = false;
            }
        }

        protected override void CleanupSpawnedAnchorObjects()
        {
            base.CleanupSpawnedAnchorObjects();
        }

        private void ConfigureSession()
        {
            const float distanceInMeters = 20.0f;
            const int maxAnchorsToFind = 20;
            SetNearDevice(distanceInMeters, maxAnchorsToFind);

            #region DEFAULT
            //TODO: Make sure to uncomment FetchLocationIds() too!

            // List<string> anchorsToFind = new List<string>();

            // if (currentAppState == AppState.CreateSessionForQuery)
            // {
            //     foreach (string id in _firebaseLoader.FetchedLocationsDict.Keys)
            //     {
            //         anchorsToFind.Add(id);
            //     }
            // }
            // {
            //     anchorsExpected = anchorsToFind.Count;
            //     SetAnchorIdsToLocate(anchorsToFind);
            // }
            #endregion
        }

        //*Coarse Reloc*
        private void ConfigureSensors()
        {
            SensorProviderHelper.locationProvider.Sensors.GeoLocationEnabled = SensorPermissionHelper.HasGeoLocationPermission();
            SensorProviderHelper.locationProvider.Sensors.WifiEnabled = SensorPermissionHelper.HasWifiPermission();
            //SensorProviderHelper.locationProvider.Sensors.BluetoothEnabled = SensorPermissionHelper.HasBluetoothPermission();
            //SensorProviderHelper.locationProvider.Sensors.KnownBeaconProximityUuids = CoarseRelocSettings.KnownBluetoothProximityUuids;
        }
        //*************

        private void TogglePlaneVisualizer(bool isActive)
        {
            GameObject planeVisualizerPrefab = FindObjectOfType<XRCameraPicker>().ARFoundationCameraTree.GetComponent<ARPlaneManager>().planePrefab;
            planeVisualizerPrefab.GetComponent<ARPlaneMeshVisualizer>().enabled = isActive;
            planeVisualizerPrefab.GetComponent<LineRenderer>().enabled = isActive;
            planeVisualizerPrefab.GetComponent<MeshRenderer>().enabled = isActive;
        }

        private async Task StopDemo()
        {
            try
            {
                Debug.Log("Stopping Demo");
                CanSave = false;
                isAdvancingDemo = false;

                if (advanceDemoTask != null && advanceDemoTask.Status == TaskStatus.Running)
                    await advanceDemoTask;

                if (currentWatcher != null)
                {
                    currentWatcher.Stop();
                    currentWatcher = null;
                }

                if (CloudManager != null && CloudManager.Session != null)
                {
                    if (CloudManager.IsSessionStarted)
                        CloudManager.StopSession();
                    else
                        CloudManager.Session.Stop();
                }

                SensorProviderHelper.locationProvider = null;
                currentCloudAnchor = null;
                isSavePressed = false;
                CleanupSpawnedAnchorObjects();
                spawnedObject = null;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public async void ToggleCreateMode(bool isCreateMode)
        {
            Debug.Log("Toggling Create Mode: " + isCreateMode);
            await StopDemo();
            this.isCreateMode = isCreateMode;

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                currentAppState = AppState.FirebaseInit;

                if (isCreateMode)
                    createLocationButton.targetGraphic.color = new Color32(0, 255, 255, 200);
                else
                    createLocationButton.targetGraphic.color = new Color32(255, 255, 255, 170);

                feedbackBox.gameObject.SetActive(isCreateMode);
                saveButton.gameObject.SetActive(false);
                firebaseLoader.ToggleLocationDataUI(false);
                TogglePlaneVisualizer(isCreateMode);
                isErrorActive = false;
                StartDemo();
            });
        }

        public void OnSceneChangeButton(string sceneName)
        {
            try
            {
                Debug.Log("Scene change to: " + sceneName);

                StopDemo().Wait(2500);
                firebaseLoader.CleanupObjects();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
            finally
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
