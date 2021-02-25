using Firebase.Database;
using Firebase.Storage;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine.Events;
using Lean.Touch;
using System;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class FirebaseLoader : FirebaseManager
    {
        [Header("Firebase Loader")]
        [SerializeField] private GameObject TempLoadingPrefab;
        [Header("Location Data UI")]
        [SerializeField] private InputField locationNameInput;
        [SerializeField] private InputField locationDescInput;
        [SerializeField] private Button locationSaveButton;
        [SerializeField] private LocationCreatedEvent onLocationCreated;
        [System.Serializable]
        public class LocationCreatedEvent : UnityEvent { }

        private string selectedUserId;
        private string anchorIdToWrite;
        private string previousFilepath = "";

        public string AnchorIdToWrite { get => anchorIdToWrite; set => anchorIdToWrite = value; }
        public Dictionary<string, GameObject> tempObjects = new Dictionary<string, GameObject>();


        protected override void Start()
        {
            selectedUserId = PlayerPrefs.GetString(SELECTED_USER_ID_PP_KEY, "");
            if (PlayerPrefs.HasKey(TRANSLATE_FILEPATH_PP_KEY))
                previousFilepath = PlayerPrefs.GetString(TRANSLATE_FILEPATH_PP_KEY, "");

            base.Start();
        }

        protected override async Task OnFirebaseInitialized()
        {
            try
            {
                await FetchUserLocationAnchorIdsAsync(selectedUserId);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                SplashText(ex.Message, Color.red);
            }

            IsReady = true;

            if (isPreloadUserModels)
                UnityDispatcher.InvokeOnAppThread(() => PreLoadAllModelsForUser());
        }

        public void ToggleLocationDataUI(bool active)
        {
            locationSaveButton.transform.parent.gameObject.SetActive(active);
        }

        public void OnWriteButtonPressed()
        {
            WriteAnchorIDToFirebaseAsync(AnchorIdToWrite, locationNameInput.text, locationDescInput.text);
            ToggleLocationDataUI(false);
        }

        public void WriteAnchorIDToFirebaseAsync(string anchorId, string locationName, string locationDesc)
        {
            Dictionary<string, object> childUpdates = new Dictionary<string, object>();
            childUpdates[anchorId] = new LocationPayload(locationName, locationDesc, previousFilepath).ToDictionary();

            FirebaseDatabase.DefaultInstance.GetReference("users" + "/" + UserId + "/" + "locations")
                .UpdateChildrenAsync(childUpdates)
                .ContinueWith(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        Debug.LogError(task.Exception);
                        SplashText(task.Exception.Message, Color.red);
                        return;
                    };

                    Debug.Log("LocationID Written To Firebase --> Consuming Token");
                    onLocationCreated.Invoke();
                });
        }

        protected override void PreLoadAllModelsForUser()
        {
            foreach (string locationId in querriedLocationAnchorIds)
            {
                Debug.Log("Preload " + selectedUserId + " - " + locationId);
                LoadOrMoveUserModel(locationId);
            }
        }

        public void LoadOrMoveUserModel(string anchorId) { LoadOrMoveUserModel(null, anchorId, false); }

        public void LoadOrMoveUserModel(CloudSpatialAnchor cloudAnchor) { LoadOrMoveUserModel(cloudAnchor, string.Empty, true); }

        private void LoadOrMoveUserModel(CloudSpatialAnchor cloudAnchor, string anchorId, bool setActive)
        {
            if (cloudAnchor != null)
                anchorId = cloudAnchor.Identifier;

            if (spawnedObjects.ContainsKey(anchorId) && spawnedObjects[anchorId] != null && cloudAnchor != null)
            {
                ModelData modelData = spawnedObjects[anchorId];
                SpatialAnchorExtensions.ApplyCloudAnchor(modelData.obj, cloudAnchor);
                modelData.obj.transform.position += modelData.positionOffset;
                modelData.obj.transform.rotation *= modelData.rotationOffset;
                modelData.obj.SetActive(setActive);

                if (tempObjects.ContainsKey(anchorId))
                    tempObjects[anchorId].SetActive(false);
            }
            else if (!spawnedObjects.ContainsKey(anchorId))
            {
                if (!hasAnchorLocations)
                {
                    Debug.LogError("Attempted to load model with no qurried locations for user");
                    return;
                }

                if (cloudAnchor != null)
                {
                    try
                    {
                        Pose cloudPose = cloudAnchor.GetPose();
                        tempObjects.Add(anchorId, Instantiate(TempLoadingPrefab, cloudPose.position, cloudPose.rotation));
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError(e.Message + "\n" + e.StackTrace);
                    }
                }

                spawnedObjects.Add(anchorId, null);
                LoadModelFromFirebase(cloudAnchor, anchorId, setActive);
            }
            else
            {
                Debug.LogWarning("Attempted to move model while loading");
            }
        }

        private void LoadModelFromFirebase(CloudSpatialAnchor cloudAnchor, string locationId, bool setActive = true)
        {
            var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            FirebaseDatabase.DefaultInstance.GetReference("users" + "/" + selectedUserId + "/locations/" + locationId)
            .GetValueAsync()
            .ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError(task.Exception);
                    return;
                }

                DataSnapshot locationSnapshot = task.Result;

                if (locationSnapshot.HasChild("filePath"))
                {
                    string filePath = locationSnapshot.Child("filePath").Value.ToString();

                    FirebaseStorage.DefaultInstance.GetReference(filePath)
                    .GetDownloadUrlAsync()
                    .ContinueWith(task2 =>
                    {
                        if (task2.IsFaulted || task2.IsCanceled)
                        {
                            Debug.Log("Error Getting URL for File Path: " + filePath);
                            Debug.LogError(task2.Exception);
                            foreach (Exception e in task2.Exception.InnerExceptions)
                            {
                                Debug.LogError(e.Message + "\n" + e.StackTrace);
                            }
                            return;
                        }
                        
                        string uri = task2.Result.AbsoluteUri;
                        LoadGltf(cloudAnchor, uri, locationId, setActive);
                    }, taskScheduler);
                }
            });
        }

        private GLTFast.UninterruptedDeferAgent gltfDeferAgent = null;

        private void LoadGltf(CloudSpatialAnchor cloudAnchor, string modelUrl, string locationId, bool setActive)
        {
            if (gltfDeferAgent == null)
                gltfDeferAgent = new GLTFast.UninterruptedDeferAgent();

            var gltf = new GameObject().AddComponent<GLTFast.GltfAsset>();
            gltf.loadOnStartup = false; // prevent auto-loading

            Vector3 positionOffset = Vector3.zero; // StringToVector(firebasePosition, ',');
            // Vector3 rotVector = StringToVector(firebaseRotation, ',');
            Quaternion rotationOffset = Quaternion.identity; // Quaternion.Euler(rotVector.x, rotVector.y, rotVector.z);
            Vector3 scale = Vector3.one; // StringToVector(firebaseScale, ',');

            gltf.transform.localScale = Vector3.zero;
            gltf.gameObject.name = locationId;

            gltf.onLoadComplete += (g, isLoaded) =>
            {
                if (isLoaded)
                {
                    UnityDispatcher.InvokeOnAppThread(() =>
                    {
                        GameObject obj = g.gameObject;

                        if (cloudAnchor != null)
                        {
                            SpatialAnchorExtensions.ApplyCloudAnchor(obj, cloudAnchor);
                            obj.transform.position += positionOffset;
                            obj.transform.rotation *= rotationOffset;
                            obj.LeanScale(scale, 4f);

                            if (tempObjects.ContainsKey(cloudAnchor.Identifier))
                                tempObjects[cloudAnchor.Identifier].SetActive(false);
                        }

                        obj.AddComponent<LeanPinchScale>();
                        obj.AddComponent<LeanDragTranslate>();
                        obj.AddComponent<LeanTwistRotate>();
                        obj.SetActive(setActive);

                        spawnedObjects[locationId] = new ModelData(obj, positionOffset, rotationOffset, scale);
                        Debug.Log("Model loaded\t- " + locationId);
                    });
                }
                else
                {
                    Debug.LogError("Failed to load\t- " + locationId);
                }
            };

            Debug.Log("GLTFast - Loading model...\t- " + locationId);
            gltf.Load(modelUrl, null, gltfDeferAgent);
        }

        private Vector3 StringToVector(string str, char del)
        {
            Vector3 vector;
            string[] vals = str.Split(del);

            vector.x = float.Parse(vals[0]);
            vector.y = float.Parse(vals[1]);
            vector.z = float.Parse(vals[2]);
            return vector;
        }

        public void CleanupSpawnedModels()
        {
            foreach (var entry in spawnedObjects)
            {
                if (entry.Value != null && entry.Value.obj != null)
                    Destroy(entry.Value.obj);
            }
            foreach (var entry in tempObjects)
            {
                if (entry.Value != null)
                    Destroy(entry.Value);
            }
            spawnedObjects.Clear();
            tempObjects.Clear();
        }
    }
}
