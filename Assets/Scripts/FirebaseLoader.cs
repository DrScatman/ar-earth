using Firebase.Database;
using Firebase.Storage;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Lean.Touch;
using System;
using UnityGLTF;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Firebase.Extensions;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class FirebaseLoader : FirebaseManager
    {
        [Header("Firebase Loader")]
        [SerializeField] private GameObject TempLoadingPrefab;
        [SerializeField] private List<GameObject> loadingGFX = new List<GameObject>();
        [SerializeField] private MaxModelsLocatedEvent onMaxModelsLocated;
        [Header("Location Data UI")]
        [SerializeField] private InputField locationNameInput;
        [SerializeField] private InputField locationDescInput;
        [SerializeField] private Button locationSaveButton;
        [SerializeField] private LocationCreatedEvent onLocationCreated;
        [Serializable]
        public class LocationCreatedEvent : UnityEvent { }
        [Serializable]
        public class MaxModelsLocatedEvent : UnityEvent { }

        private int numMyModelsFound;
        private Dictionary<string, GameObject> SpawnedObjects { get => SpawnedObjectsSingleton.Instance.SpawnedObjects; }
        private static readonly System.Random rnd = new System.Random();

        public string AnchorIdToWrite { get; set; }
        public Dictionary<string, GameObject> tempObjects = new Dictionary<string, GameObject>();


        protected override async Task OnFirebaseInitialized()
        {
            if (!IsAuthenticated)
            {
                Debug.LogError("Not Authenticated - Logging Out...");
                SceneManager.LoadSceneAsync("Login UI");
            }

            await FetchUserLocationAnchorIdsAsync(UserId, true);
        }

        #region TEST
        // private bool hasLoaded;
        // protected override void Update()
        // {
        //     base.Update();

        //     if (IsReady && !hasLoaded)
        //     {
        //         Debug.Log("Preloading");
        //         hasLoaded = true;

        //         FetchLocationIdsAsync().ContinueWith(task =>
        //         {
        //             if (task.IsFaulted || task.IsCanceled)
        //             {
        //                 Debug.LogError(task.Exception);
        //                 return;
        //             }

        //             UnityDispatcher.InvokeOnAppThread(() => PreLoadAllModelsForUser());
        //         });
        //     }
        // }
        #endregion

        public void ToggleLocationDataUI(bool active)
        {
            locationSaveButton.transform.parent.gameObject.SetActive(active);
        }

        public void OnWriteButtonPressed()
        {
            if (!string.IsNullOrEmpty(locationNameInput.text) && !locationNameInput.text.Contains("-")
                && (string.IsNullOrEmpty(locationDescInput.text) || !locationDescInput.text.Contains("-")))
            {
                WriteAnchorIDToFirebaseAsync(AnchorIdToWrite, locationNameInput.text, locationDescInput.text);
                ToggleLocationDataUI(false);
            }
            else
            {
                if (string.IsNullOrEmpty(locationNameInput.text))
                    SplashText("Enter A Unique Name For The Location", Color.red);
                else
                    SplashText("Location Name & Description Can't Contain '-'", Color.red);
            }
        }

        public void WriteAnchorIDToFirebaseAsync(string anchorId, string locationName, string locationDesc)
        {
            Dictionary<string, object> childUpdates = new Dictionary<string, object>();
            LocationPayload locationPayload = new LocationPayload(locationName, locationDesc);

            if (TranslateModel != null && !string.IsNullOrEmpty(TranslateModel.filePath))
            {
                childUpdates[anchorId] = GetFullPayload(locationPayload, TranslateModel);
            }
            else
            {
                childUpdates[anchorId] = locationPayload.ToDictionary();
            }

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

                    PlayerPrefs.SetString(NEW_LOCATIONID_PP_KEY, anchorId);
                    onLocationCreated.Invoke();
                });
        }

        public void LoadOrMoveUserModel(CloudSpatialAnchor cloudAnchor, bool setActive = true)
        {
            string anchorId = cloudAnchor.Identifier;

            if (SpawnedObjects.ContainsKey(anchorId) && SpawnedObjects[anchorId] != null && cloudAnchor != null)
            {
                GameObject model = SpawnedObjects[anchorId];
                SpatialAnchorExtensions.ApplyCloudAnchor(model, cloudAnchor);
                model.SetActive(setActive);

                if (tempObjects.ContainsKey(anchorId))
                    tempObjects[anchorId].SetActive(false);
            }
            else if (!SpawnedObjects.ContainsKey(anchorId))
            {
                LoadModelFromFirebase(cloudAnchor, setActive);
            }
            else
            {
                Debug.LogWarning("Attempted to move model while loading");
            }
        }

        private async void LoadModelFromFirebase(CloudSpatialAnchor cloudAnchor, bool setActive = true)
        {
            try
            {
                string anchorId = cloudAnchor.Identifier;
                string locatedUserId = UserId;
                SpawnedObjects.Add(anchorId, null);

                if (cloudAnchor.AppProperties.ContainsKey(DemoScriptBase.UUID_ANCHOR_PROPERTY_KEY))
                    locatedUserId = cloudAnchor.AppProperties[DemoScriptBase.UUID_ANCHOR_PROPERTY_KEY];

                var locationsDict = FetchedLocationsDict;

                if (!locationsDict.ContainsKey(anchorId) || locationsDict[anchorId].ModelPayload == null)
                    locationsDict = await FetchUserLocationAnchorIdsAsync(locatedUserId, true);

                if (locationsDict.Count > 0 && locationsDict.ContainsKey(anchorId) && locationsDict[anchorId].HasModel)
                {
                    if (locatedUserId == UserId)
                    {
                        numMyModelsFound++;

                        if (numMyModelsFound >= locationsDict.Count)
                            onMaxModelsLocated.Invoke();
                    }

                    UnityDispatcher.InvokeOnAppThread(() =>
                    {
                        Pose cloudPose = cloudAnchor.GetPose();
                        tempObjects.Add(anchorId, Instantiate(TempLoadingPrefab, cloudPose.position, cloudPose.rotation));

                        if (numModelsLoading < 2 && loadingGFX.Count > 0)
                        {
                            int r = rnd.Next(loadingGFX.Count);
                            Instantiate(loadingGFX[r], cloudPose.position, cloudPose.rotation);
                        }
                    });

                    var model = locationsDict[anchorId].ModelPayload;
                    string filePath = model.filePath;

                    StartCoroutine(DeferLoad(cloudAnchor, filePath, setActive));
                    //SplashText($"Found: {model.ToString()}", Color.cyan);
                }
                else
                {
                    SpawnedObjects.Remove(anchorId);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                SpawnedObjects.Remove(cloudAnchor.Identifier);
                SplashText(e.Message, Color.red);
            }
        }

        private System.Collections.IEnumerator DeferLoad(CloudSpatialAnchor cloudAnchor, string filePath, bool setActive)
        {
            yield return new WaitUntil(() => numModelsLoading < MAX_LOADING_NUM);
            numModelsLoading++;

            FirebaseStorage.DefaultInstance.GetReference(filePath).GetDownloadUrlAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError(task.Exception);
                    SplashText(task.Exception.GetBaseException().Message, Color.red);
                    numModelsLoading--;
                }

                if (this != null)
                    LoadUnityGLTF(cloudAnchor, task.Result.AbsoluteUri, setActive);
            });

            yield return null;
        }

        private int numModelsLoading;
        private static readonly int MAX_LOADING_NUM = 2;

        private void LoadUnityGLTF(CloudSpatialAnchor cloudAnchor, string uri, bool setActive, int retries = 6)
        {
            var gltf = new GameObject().AddComponent<GLTFComponent>();
            gltf.loadOnStart = false;
            gltf.Multithreaded = true;
            gltf.PlayAnimationOnLoad = true;
            gltf.AppendStreamingAssets = true;
            //gltf.MaximumLod = 600;
            gltf.Collider = GLTFSceneImporter.ColliderType.Box;
            gltf.GLTFUri = uri;

            //int maxLoad = Math.Max(numModelsLoading, GetNumModelsToLoad());
            gltf.gameObject.AddComponent<AsyncCoroutineHelper>().BudgetPerFrameInSeconds = ((((retries > 0 ? retries : 0.1f) / 6) * 0.03f) / numModelsLoading);

            string anchorId = cloudAnchor.Identifier;
            gltf.gameObject.name = anchorId;
            gltf.transform.localScale = Vector3.zero;

            gltf.Load().ContinueWith((task) =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError(task.Exception);

                    if (retries > 0 && (task.Exception.GetBaseException() is OutOfMemoryException || task.Exception.GetBaseException() is System.Net.Http.HttpRequestException))
                    {
                        UnityDispatcher.InvokeOnAppThread(() =>
                        {
                            Destroy(gltf.gameObject);
                            LoadUnityGLTF(cloudAnchor, uri, setActive, retries - 1);
                        });
                        return;
                    }

                    SpawnedObjects.Remove(cloudAnchor.Identifier);
                    numModelsLoading--;
                    SplashText(task.Exception.GetBaseException().Message, Color.red);
                    return;
                }

                Debug.Log("Model loaded\t- " + anchorId);
                UnityDispatcher.InvokeOnAppThread(() =>
                {
                    GameObject obj = gltf.gameObject;
                    anchorId = cloudAnchor.Identifier;
                    SpatialAnchorExtensions.ApplyCloudAnchor(obj, cloudAnchor);

                    if (tempObjects.ContainsKey(anchorId))
                        tempObjects[anchorId].SetActive(false);

                    obj.SetActive(setActive);
                    obj.LeanScale(Vector3.one, 4f).setOnComplete(() => AddLeanTouch(obj, false));

                    SpawnedObjects[anchorId] = obj;
                    gltf.transform.SetParent(SpawnedObjectsSingleton.Instance.transform, true);
                    DontDestroyOnLoad(SpawnedObjectsSingleton.Instance);

                    numModelsLoading--;
                });
            });

            Debug.Log("GLTFUnity - Loading model...\t- " + anchorId);
        }

        private List<string> locationsToLoad = new List<string>();

        private int GetNumModelsToLoad()
        {
            foreach (var loc in FetchedLocationsDict)
            {
                if (loc.Value != null && loc.Value.HasModel && !locationsToLoad.Contains(loc.Key)
                    && (!SpawnedObjects.ContainsKey(loc.Key) || SpawnedObjects[loc.Key] == null))
                    locationsToLoad.Add(loc.Key);
            }

            return locationsToLoad.Count;
        }

        private void AddLeanTouch(GameObject obj, bool addCollider)
        {
            if (addCollider)
                FitColliderToChildren(obj);

            obj.AddComponent<LeanSelectable>().DeselectOnUp = true;
            obj.AddComponent<LeanPinchScale>().Use.IgnoreStartedOverGui = false;
            obj.AddComponent<LeanDragTranslate>().Use.IgnoreStartedOverGui = false;
            obj.AddComponent<LeanTwistRotateAxis>().Use.IgnoreStartedOverGui = false;
        }

        // private void LoadGltFast(CloudSpatialAnchor cloudAnchor, string modelUrl, bool setActive)
        // {
        //     string anchorId = cloudAnchor.Identifier;

        //     var gltf = new GameObject().AddComponent<GltfAsset>();
        //     gltf.loadOnStartup = false; // prevent auto-loading
        //     gltf.gameObject.name = anchorId;
        //     gltf.transform.localScale = Vector3.zero;

        //     gltf.onLoadComplete += (g, isLoaded) =>
        //     {
        //         if (isLoaded)
        //         {
        //             UnityDispatcher.InvokeOnAppThread(() =>
        //             {
        //                 GameObject obj = g.gameObject;

        //                 if (cloudAnchor != null)
        //                 {
        //                     SpatialAnchorExtensions.ApplyCloudAnchor(obj, cloudAnchor);

        //                     if (tempObjects.ContainsKey(cloudAnchor.Identifier))
        //                         tempObjects[cloudAnchor.Identifier].SetActive(false);
        //                 }

        //                 obj.SetActive(setActive);
        //                 obj.LeanScale(Vector3.one, 4f).setOnComplete(() => AddLeanTouch(obj, true));

        //                 SpawnedObjects[anchorId] = obj;
        //                 gltf.transform.SetParent(SpawnedObjectsSingleton.Instance.transform, true);
        //                 DontDestroyOnLoad(SpawnedObjectsSingleton.Instance);
        //                 Debug.Log("Model loaded\t- " + anchorId);
        //             });
        //         }
        //         else
        //         {
        //             Debug.LogError("Failed to load\t- " + anchorId);
        //         }
        //     };

        //     Debug.Log("GLTFast - Loading model...\t- " + anchorId);

        //     if (GetNumModelsToLoad() > 1)
        //         gltf.Load(modelUrl, null, GetTimeBudgetDeferAgent());
        //     else
        //         gltf.Load(modelUrl, null, GetUninterruptedDeferAgent());
        // }

        // private IDeferAgent timeDeferAgent = null;
        // private IDeferAgent uninterruptedDeferAgent = null;

        // private IDeferAgent GetTimeBudgetDeferAgent()
        // {
        //     if (timeDeferAgent == null)
        //     {
        //         timeDeferAgent = gameObject.AddComponent<TimeBudgetPerFrameDeferAgent>();
        //     }

        //     return timeDeferAgent;
        // }

        // private IDeferAgent GetUninterruptedDeferAgent()
        // {
        //     if (uninterruptedDeferAgent == null)
        //     {
        //         uninterruptedDeferAgent = new UninterruptedDeferAgent();
        //     }

        //     return uninterruptedDeferAgent;
        // }

        private void FitColliderToChildren(GameObject parentObject)
        {
            BoxCollider bc = parentObject.AddComponent<BoxCollider>();
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer render in parentObject.GetComponentsInChildren<Renderer>())
            {
                if (hasBounds)
                {
                    bounds.Encapsulate(render.bounds);
                }
                else
                {
                    bounds = render.bounds;
                    hasBounds = true;
                }
            }
            if (hasBounds)
            {
                bc.center = bounds.center - parentObject.transform.position;
                bc.size = bounds.size;
            }
            else
            {
                bc.size = bc.center = Vector3.zero;
                bc.size = Vector3.zero;
            }
        }

        public void CleanupObjects()
        {
            List<string> loadingKeys = new List<string>();

            foreach (var entry in SpawnedObjects)
            {
                if (entry.Value != null)
                    entry.Value.SetActive(false);
                else if (entry.Value == null && entry.Key != null)
                    loadingKeys.Add(entry.Key);
            }

            loadingKeys.ForEach(k => SpawnedObjects.Remove(k));

            foreach (var entry in tempObjects)
            {
                if (entry.Value != null)
                    Destroy(entry.Value);
            }

            tempObjects.Clear();
        }

        public bool IsSpawnedObjectSelected()
        {
            try
            {
                if (SpawnedObjects != null && SpawnedObjects.Values != null && SpawnedObjects.Values.Count > 0)
                {
                    foreach (GameObject obj in SpawnedObjects.Values)
                    {
                        LeanSelectable selectable = obj.GetComponent<LeanSelectable>();

                        if (selectable != null && selectable.IsSelectedRaw)
                            return true;
                    }
                }
            }
            catch (Exception) { return false; }
            return false;
        }

        public void RemoveLoadingModels()
        {
            List<string> loadingKeys = new List<string>();

            foreach (var entry in SpawnedObjects)
            {
                if (entry.Value == null && entry.Key != null)
                    loadingKeys.Add(entry.Key);
            }

            loadingKeys.ForEach(k => SpawnedObjects.Remove(k));
        }
    }
}
