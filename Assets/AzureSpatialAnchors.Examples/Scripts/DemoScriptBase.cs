// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public abstract class DemoScriptBase : InputInteractionBase
    {
        #region Member Variables
        protected Task advanceDemoTask = null;
        protected bool isErrorActive = false;
        protected Text feedbackBox;
        protected readonly List<string> anchorIdsToLocate = new List<string>();
        protected AnchorLocateCriteria anchorLocateCriteria = null;
        protected CloudSpatialAnchor currentCloudAnchor;
        protected CloudSpatialAnchorWatcher currentWatcher;
        protected GameObject spawnedObject = null;
        protected Material spawnedObjectMat = null;
        protected bool enableAdvancingOnSelect = true;
        protected bool isAdvancingDemo = true;
        protected bool CanSave { get; set; }
        public static readonly string UUID_ANCHOR_PROPERTY_KEY = "FB_UUID";
        #endregion // Member Variables

        #region Unity Inspector Variables
        [SerializeField]
        [Tooltip("The prefab used to represent an anchored object.")]
        private GameObject anchoredObjectPrefab = null;
        public FirebaseLoader firebaseLoader;

        [SerializeField]
        [Tooltip("SpatialAnchorManager instance to use for this demo. This is required.")]
        private SpatialAnchorManager cloudManager = null;
        #endregion // Unity Inspector Variables

        /// <summary>
        /// Destroying the attached Behaviour will result in the game or Scene
        /// receiving OnDestroy.
        /// </summary>
        /// <remarks>OnDestroy will only be called on game objects that have previously been active.</remarks>
        public override void OnDestroy()
        {
            if (CloudManager != null)
            {
                if (CloudManager.IsSessionStarted)
                    CloudManager.StopSession();
            }

            if (currentWatcher != null)
            {
                currentWatcher.Stop();
                currentWatcher = null;
            }

            CleanupSpawnedAnchorObjects();

            // Pass to base for final cleanup
            base.OnDestroy();
        }

        public virtual bool SanityCheckAccessConfiguration()
        {
            if (string.IsNullOrWhiteSpace(CloudManager.SpatialAnchorsAccountId)
                || string.IsNullOrWhiteSpace(CloudManager.SpatialAnchorsAccountKey)
                || string.IsNullOrWhiteSpace(CloudManager.SpatialAnchorsAccountDomain))
            {
                return false;
            }

            string iso2 = "";

            if (PlayerPrefs.HasKey(LocationHelper.COUNTRY_CODE_PP_KEY))
                iso2 = PlayerPrefs.GetString(LocationHelper.COUNTRY_CODE_PP_KEY, "");
            else if (System.Globalization.RegionInfo.CurrentRegion != null)
                iso2 = System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName;

            iso2 = iso2.ToUpper();

            if (iso2 == "IN" || iso2 == "IR" || iso2 == "BD" || iso2 == "PH" || iso2 == "SG"
                        || iso2 == "ID" || iso2 == "CN" || iso2 == "JP" || iso2 == "NP")
            {
                Debug.Log("Azure Resource -> SEA");
                CloudManager.SpatialAnchorsAccountId = "98e82696-a3a8-4f1e-b3c9-ee28a0bdbc77";
                CloudManager.SpatialAnchorsAccountKey = "JT1v+Tm+xjDR2RjtnciQ1aRRhmw+e/pTaldpZBLW/DQ=";
                CloudManager.SpatialAnchorsAccountDomain = "southeastasia.mixedreality.azure.com";
            }
            else if (iso2 == "EG" || iso2 == "TR" || iso2 == "RU" || iso2 == "DE" || iso2 == "FR"
                        || iso2 == "GB" || iso2 == "ZA")
            {
                Debug.Log("Azure Resource -> NEU");
                CloudManager.SpatialAnchorsAccountId = "3126368d-141c-48da-bdb0-b14dfbe1556e";
                CloudManager.SpatialAnchorsAccountKey = "3R8u0VzZoZSaaRF8hlzqTIacYlZThbKWsEOR3GyKbz0=";
                CloudManager.SpatialAnchorsAccountDomain = "northeurope.mixedreality.azure.com";
            }
            else
            {
                Debug.Log("Azure Resource -> US");
            }

            return true;
        }

        /// <summary>
        /// Start is called on the frame when a script is enabled just before any
        /// of the Update methods are called the first time.
        /// </summary>
        public override void Start()
        {
            feedbackBox = XRUXPicker.Instance.GetFeedbackText();
            if (feedbackBox == null)
            {
                Debug.Log($"{nameof(feedbackBox)} not found in scene by XRUXPicker.");
                Destroy(this);
                return;
            }

            if (CloudManager == null)
            {
                Debug.Break();
                feedbackBox.text = $"{nameof(CloudManager)} reference has not been set. Make sure it has been added to the scene and wired up to {this.name}.";
                return;
            }

            if (!SanityCheckAccessConfiguration())
            {
                feedbackBox.text = $"{nameof(SpatialAnchorManager.SpatialAnchorsAccountId)}, {nameof(SpatialAnchorManager.SpatialAnchorsAccountKey)} and {nameof(SpatialAnchorManager.SpatialAnchorsAccountDomain)} must be set on {nameof(SpatialAnchorManager)}";
            }

            if (AnchoredObjectPrefab == null)
            {
                feedbackBox.text = "CreationTarget must be set on the demo script.";
                return;
            }

            CloudManager.SessionUpdated += CloudManager_SessionUpdated;
            CloudManager.AnchorLocated += CloudManager_AnchorLocated;
            CloudManager.LocateAnchorsCompleted += CloudManager_LocateAnchorsCompleted;
            CloudManager.LogDebug += CloudManager_LogDebug;
            CloudManager.Error += CloudManager_Error;

            anchorLocateCriteria = new AnchorLocateCriteria();

            base.Start();
        }

        /// <summary>
        /// Advances the demo.
        /// </summary>
        /// <returns>
        /// A <see cref="Task"/> that represents the operation.
        /// </returns>
        public abstract Task AdvanceDemoAsync();

        /// <summary>
        /// This version only exists for Unity to wire up a button click to.
        /// If calling from code, please use the Async version above.
        /// </summary>
        public virtual async void AdvanceDemo()
        {
            try
            {
                isAdvancingDemo = true;
                advanceDemoTask = AdvanceDemoAsync();
                await advanceDemoTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{nameof(DemoScriptBase)} - Error in {nameof(AdvanceDemo)}: {ex.Message} {ex.StackTrace}");
                feedbackBox.text = $"Demo failed, check debugger output for more information";
            }
        }

        public virtual Task EnumerateAllNearbyAnchorsAsync(float distanceInMeters) { throw new NotImplementedException(); }

        public async void EnumerateAllNearbyAnchors(float distanceInMeters)
        {
            try
            {
                await EnumerateAllNearbyAnchorsAsync(distanceInMeters);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{nameof(DemoScriptBase)} - Error in {nameof(EnumerateAllNearbyAnchors)}: === {ex.GetType().Name} === {ex.ToString()} === {ex.Source} === {ex.Message} {ex.StackTrace}");
                feedbackBox.text = $"Enumeration failed, check debugger output for more information";
            }
        }

        /// <summary>
        /// Cleans up spawned objects.
        /// </summary>
        protected virtual void CleanupSpawnedAnchorObjects()
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                if (spawnedObject != null)
                {
                    Destroy(spawnedObject);
                    spawnedObject = null;
                }

                if (spawnedObjectMat != null)
                {
                    Destroy(spawnedObjectMat);
                    spawnedObjectMat = null;
                }
            });
        }

        protected CloudSpatialAnchorWatcher CreateWatcher()
        {
            if ((CloudManager != null) && (CloudManager.Session != null))
            {
                return CloudManager.Session.CreateWatcher(anchorLocateCriteria);
            }
            else
            {
                return null;
            }
        }

        protected void SetAnchorIdsToLocate(IEnumerable<string> anchorIds)
        {
            if (anchorIds == null)
            {
                throw new ArgumentNullException(nameof(anchorIds));
            }

            ResetAnchorIdsToLocate();
            anchorIdsToLocate.AddRange(anchorIds);
            anchorLocateCriteria.Identifiers = anchorIdsToLocate.ToArray();
        }

        protected void ResetAnchorIdsToLocate()
        {
            anchorIdsToLocate.Clear();
            anchorLocateCriteria.Identifiers = new string[0];
        }

        protected void SetNearbyAnchor(CloudSpatialAnchor nearbyAnchor, float DistanceInMeters, int MaxNearAnchorsToFind)
        {
            if (nearbyAnchor == null)
            {
                anchorLocateCriteria.NearAnchor = new NearAnchorCriteria();
                return;
            }

            NearAnchorCriteria nac = new NearAnchorCriteria();
            nac.SourceAnchor = nearbyAnchor;
            nac.DistanceInMeters = DistanceInMeters;
            nac.MaxResultCount = MaxNearAnchorsToFind;

            anchorLocateCriteria.NearAnchor = nac;
        }

        protected void SetNearDevice(float DistanceInMeters, int MaxAnchorsToFind)
        {
            NearDeviceCriteria nearDeviceCriteria = new NearDeviceCriteria();
            nearDeviceCriteria.DistanceInMeters = DistanceInMeters;
            nearDeviceCriteria.MaxResultCount = MaxAnchorsToFind;

            anchorLocateCriteria.NearDevice = nearDeviceCriteria;
        }

        protected void SetGraphEnabled(bool UseGraph, bool JustGraph = false)
        {
            anchorLocateCriteria.Strategy = UseGraph ?
                                            (JustGraph ? LocateStrategy.Relationship : LocateStrategy.AnyStrategy) :
                                            LocateStrategy.VisualInformation;
        }

        /// <summary>
        /// Bypassing the cache will force new queries to be sent for objects, allowing
        /// for refined poses over time.
        /// </summary>
        /// <param name="BypassCache"></param>
        public void SetBypassCache(bool BypassCache)
        {
            anchorLocateCriteria.BypassCache = BypassCache;
        }

        /// <summary>
        /// Gets the color of the current demo step.
        /// </summary>
        /// <returns><see cref="Color"/>.</returns>
        protected abstract Color GetStepColor();

        /// <summary>
        /// Determines whether the demo is in a mode that should place an object.
        /// </summary>
        /// <returns><c>true</c> to place; otherwise, <c>false</c>.</returns>
        protected abstract bool IsPlacingObject();

        /// <summary>
        /// Moves the specified anchored object.
        /// </summary>
        /// <param name="objectToMove">The anchored object to move.</param>
        /// <param name="worldPos">The world position.</param>
        /// <param name="worldRot">The world rotation.</param>
        /// <param name="cloudSpatialAnchor">The cloud spatial anchor.</param>
        protected virtual void MoveAnchoredObject(GameObject objectToMove, Vector3 worldPos, Quaternion worldRot, CloudSpatialAnchor cloudSpatialAnchor = null)
        {
            // Get the cloud-native anchor behavior
            CloudNativeAnchor cna = objectToMove.GetComponent<CloudNativeAnchor>();

            // Warn and exit if the behavior is missing
            if (cna == null)
            {
                Debug.LogWarning($"The object {objectToMove.name} is missing the {nameof(CloudNativeAnchor)} behavior.");
                return;
            }

            // Is there a cloud anchor to apply
            if (cloudSpatialAnchor != null)
            {
                // Yes. Apply the cloud anchor, which also sets the pose.
                cna.CloudToNative(cloudSpatialAnchor);
            }
            else
            {
                // No. Just set the pose.
                cna.SetPose(worldPos, worldRot);
            }
        }

        /// <summary>
        /// Called when a cloud anchor is located.
        /// </summary>
        /// <param name="args">The <see cref="AnchorLocatedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnCloudAnchorLocated(AnchorLocatedEventArgs args)
        {
            // To be overridden.
        }

        /// <summary>
        /// Called when cloud anchor location has completed.
        /// </summary>
        /// <param name="args">The <see cref="LocateAnchorsCompletedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnCloudLocateAnchorsCompleted(LocateAnchorsCompletedEventArgs args)
        {
            Debug.Log("Locate pass complete");
        }

        /// <summary>
        /// Called when the current cloud session is updated.
        /// </summary>
        protected virtual void OnCloudSessionUpdated()
        {
            // To be overridden.
        }

        /// <summary>
        /// Called when gaze interaction occurs.
        /// </summary>
        protected override void OnGazeInteraction()
        {
#if WINDOWS_UWP || UNITY_WSA
            // HoloLens gaze interaction
            if (IsPlacingObject())
            {
                base.OnGazeInteraction();
            }
#endif
        }

        /// <summary>
        /// Called when gaze interaction begins.
        /// </summary>
        /// <param name="hitPoint">The hit point.</param>
        /// <param name="target">The target.</param>
        protected override void OnGazeObjectInteraction(Vector3 hitPoint, Vector3 hitNormal)
        {
            base.OnGazeObjectInteraction(hitPoint, hitNormal);

#if WINDOWS_UWP || UNITY_WSA
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
            SpawnOrMoveCurrentAnchoredObject(hitPoint, rotation);
#endif
        }

        /// <summary>
        /// Called when a cloud anchor is not saved successfully.
        /// </summary>
        /// <param name="exception">The exception.</param>
        protected virtual void OnSaveCloudAnchorFailed(Exception exception)
        {
            // we will block the next step to show the exception message in the UI.
            isErrorActive = true;
            Debug.LogException(exception);
            Debug.Log("Failed to save anchor " + exception.ToString());

            firebaseLoader.SplashText(exception.GetBaseException().Message, Color.red);
        }

        /// <summary>
        /// Called when a cloud anchor is saved successfully.
        /// </summary>
        protected virtual Task OnSaveCloudAnchorSuccessfulAsync()
        {
            // To be overridden.
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a select interaction occurs.
        /// </summary>
        /// <remarks>Currently only called for HoloLens.</remarks>
        protected override void OnSelectInteraction()
        {
#if WINDOWS_UWP || UNITY_WSA
            if(enableAdvancingOnSelect)
            {
                // On HoloLens, we just advance the demo.
                UnityDispatcher.InvokeOnAppThread(() => advanceDemoTask = AdvanceDemoAsync());
            }
#endif

            base.OnSelectInteraction();
        }

        /// <summary>
        /// Called when a touch object interaction occurs.
        /// </summary>
        /// <param name="hitPoint">The position.</param>
        /// <param name="target">The target.</param>
        protected override void OnSelectObjectInteraction(Vector3 hitPoint, object target)
        {
            if (IsPlacingObject())
            {
                Quaternion rotation = Quaternion.AngleAxis(0, Vector3.up);
                SpawnOrMoveCurrentAnchoredObject(hitPoint, rotation);
            }
        }

        private Vector3 fp;   //First touch position
        private Vector3 lp;   //Last touch position
        private float dragDistance;  //minimum distance for a swipe to be registered

        /// <summary>
        /// Called when a touch interaction occurs.
        /// </summary>
        /// <param name="touch">The touch.</param>
        protected override void OnTouchInteraction(Touch[] touches)
        {
            if (IsPlacingObject())
            {
                base.OnTouchInteraction(touches);
            }
        }

        /// <summary>
        /// Saves the current object anchor to the cloud.
        /// </summary>
        protected virtual async Task SaveCurrentObjectAnchorToCloudAsync(string uuidPropertyValue = null, GameObject anchoredObject = null, int retries = 5)
        {
            if (anchoredObject == null)
                anchoredObject = spawnedObject;

            // Get the cloud-native anchor behavior
            CloudNativeAnchor cna = anchoredObject.GetComponent<CloudNativeAnchor>();

            // If the cloud portion of the anchor hasn't been created yet, create it
            if (cna.CloudAnchor == null) { cna.NativeToCloud(); }

            // Get the cloud portion of the anchor
            CloudSpatialAnchor cloudAnchor = cna.CloudAnchor;

            // In this sample app we delete the cloud anchor explicitly, but here we show how to set an anchor to expire automatically
            cloudAnchor.Expiration = DateTimeOffset.Now.AddYears(1);

            if (!string.IsNullOrEmpty(uuidPropertyValue) && cloudAnchor.AppProperties != null)
                cloudAnchor.AppProperties[UUID_ANCHOR_PROPERTY_KEY] = uuidPropertyValue;

            CanSave = true;
            while (!CloudManager.IsReadyForCreate)
            {
                try
                {
                    if (!CanSave)
                        return;

                    await Task.Delay(330);
                    float createProgress = CloudManager.SessionStatus.RecommendedForCreateProgress;
                    feedbackBox.text = $"Slowly move your device to scan all sides of the location: {createProgress:0%}";
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }

            bool success = false;

            feedbackBox.text = "Saving...";

            try
            {
                // Actually save
                await CloudManager.CreateAnchorAsync(cloudAnchor);

                // Store
                currentCloudAnchor = cloudAnchor;

                // Success?
                success = currentCloudAnchor != null;

                if (success && !isErrorActive)
                {
                    // Await override, which may perform additional tasks
                    // such as storing the key in the AnchorExchanger
                    await OnSaveCloudAnchorSuccessfulAsync();
                }
                else
                {
                    if (retries > 0)
                    {
                        isErrorActive = false;
                        firebaseLoader.SplashText("Retying Save...", Color.yellow);
                        await Task.Delay(330);
                        await SaveCurrentObjectAnchorToCloudAsync(uuidPropertyValue, anchoredObject, retries - 1);
                    }
                    else
                    {
                        OnSaveCloudAnchorFailed(new Exception("Failed to save, but no exception was thrown."));
                    }
                }
            }
            catch (Exception ex)
            {
                if (retries > 0)
                {
                    isErrorActive = false;
                    firebaseLoader.SplashText("Retying Save...", Color.yellow);
                    await Task.Delay(330);
                    await SaveCurrentObjectAnchorToCloudAsync(uuidPropertyValue, anchoredObject, retries - 1);
                }
                else
                {
                    OnSaveCloudAnchorFailed(ex);
                }
            }
        }

        /// <summary>
        /// Spawns a new anchored object.
        /// </summary>
        /// <param name="worldPos">The world position.</param>
        /// <param name="worldRot">The world rotation.</param>
        /// <returns><see cref="GameObject"/>.</returns>
        protected virtual GameObject SpawnNewAnchoredObject(Vector3 worldPos, Quaternion worldRot)
        {
            // Create the prefab
            GameObject newGameObject = GameObject.Instantiate(AnchoredObjectPrefab, worldPos, worldRot);

            // Attach a cloud-native anchor behavior to help keep cloud
            // and native anchors in sync.
            newGameObject.AddComponent<CloudNativeAnchor>();

            // Set the color
            newGameObject.GetComponent<MeshRenderer>().material.color = GetStepColor();

            // Return created object
            return newGameObject;
        }

        /// <summary>
        /// Spawns a new object.
        /// </summary>
        /// <param name="worldPos">The world position.</param>
        /// <param name="worldRot">The world rotation.</param>
        /// <param name="cloudSpatialAnchor">The cloud spatial anchor.</param>
        /// <returns><see cref="GameObject"/>.</returns>
        protected virtual GameObject SpawnNewAnchoredObject(Vector3 worldPos, Quaternion worldRot, CloudSpatialAnchor cloudSpatialAnchor)
        {
            // Create the object like usual
            GameObject newGameObject = SpawnNewAnchoredObject(worldPos, worldRot);

            // If a cloud anchor is passed, apply it to the native anchor
            if (cloudSpatialAnchor != null)
            {
                CloudNativeAnchor cloudNativeAnchor = newGameObject.GetComponent<CloudNativeAnchor>();
                cloudNativeAnchor.CloudToNative(cloudSpatialAnchor);
            }

            // Set color
            newGameObject.GetComponent<MeshRenderer>().material.color = GetStepColor();

            // Return newly created object
            return newGameObject;
        }

        /// <summary>
        /// Spawns a new anchored object and makes it the current object or moves the
        /// current anchored object if one exists.
        /// </summary>
        /// <param name="worldPos">The world position.</param>
        /// <param name="worldRot">The world rotation.</param>
        protected virtual void SpawnOrMoveCurrentAnchoredObject(Vector3 worldPos, Quaternion worldRot)
        {
            // Create the object if we need to, and attach the platform appropriate
            // Anchor behavior to the spawned object
            if (spawnedObject == null)
            {
                // Use factory method to create
                spawnedObject = SpawnNewAnchoredObject(worldPos, worldRot, currentCloudAnchor);

                // Update color
                spawnedObjectMat = spawnedObject.GetComponent<MeshRenderer>().material;
            }
            else
            {
                spawnedObject.SetActive(true);
                // Use factory method to move
                MoveAnchoredObject(spawnedObject, worldPos, worldRot, currentCloudAnchor);
            }
        }

        private void CloudManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
        {
            if (args.Status == LocateAnchorStatus.Located)
            {
                Debug.LogFormat("Anchor recognized as a possible anchor {0} {1}", args.Identifier, args.Status);
                OnCloudAnchorLocated(args);
            }
        }

        private void CloudManager_LocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
        {
            OnCloudLocateAnchorsCompleted(args);
        }

        private void CloudManager_SessionUpdated(object sender, SessionUpdatedEventArgs args)
        {
            OnCloudSessionUpdated();
        }

        private void CloudManager_Error(object sender, SessionErrorEventArgs args)
        {
            isErrorActive = true;
            Debug.LogError(args.ErrorMessage);

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                try
                {
                    if (this.feedbackBox != null)
                    {
                        this.feedbackBox.text = string.Format("Error: {0}", args.ErrorMessage);
                    }
                    else
                    {
                        firebaseLoader.SplashText(args.ErrorMessage, Color.red);
                    }
                }
                catch (Exception) { }
            });
        }

        private void CloudManager_LogDebug(object sender, OnLogDebugEventArgs args)
        {
            Debug.Log(args.Message);
        }

        protected struct DemoStepParams
        {
            public Color StepColor { get; set; }
            public string StepMessage { get; set; }
        }


        #region Public Properties
        /// <summary>
        /// Gets the prefab used to represent an anchored object.
        /// </summary>
        public GameObject AnchoredObjectPrefab { get { return anchoredObjectPrefab; } }

        /// <summary>
        /// Gets the <see cref="SpatialAnchorManager"/> instance used by this demo.
        /// </summary>
        public SpatialAnchorManager CloudManager { get { return cloudManager; } }
        #endregion // Public Properties
    }
}
