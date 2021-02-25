using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Unity.Editor;
using System.Threading.Tasks;
using UnityEngine.UI;
using System;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public abstract class FirebaseManager : MonoBehaviour
    {
        [Tooltip("Debug mode")]
        [SerializeField] protected bool debug = false;
        [Tooltip("Text to display")]
        [SerializeField] private Text splashText;
        [Tooltip("Current Firebase status")]
        [SerializeField] protected Text statusText;

        protected bool isPreloadUserModels;
        protected readonly string FIREBASE_URL = "https://arearth-6503b.firebaseio.com/";
        protected static readonly Dictionary<string, ModelSpawnData> spawnedObjects = new Dictionary<string, ModelSpawnData>();
        protected List<string> querriedLocationAnchorIds = new List<string>();
        protected bool hasAnchorLocations;

        private static Firebase.Auth.FirebaseAuth auth;
        private bool isReady = false;
        private int numAnchorTokens = 0;
        private bool hasFetchedAnchorTokens;

        protected static readonly string SELECTED_USER_ID_PP_KEY = "SELECTED_USER_ID";
        protected readonly string ANCHOR_ID_PLAYER_PREFS_KEY = "CURRENT_ANCHOR_ID";
        protected static readonly string LOGIN_EMAIL_PP_KEY = "LOGIN_EMAIL";
        protected static readonly string LOGIN_PASSWORD_PP_KEY = "LOGIN_PASSWORD";
        public static readonly string IS_CREATE_MODE_PP_KEY = "IS_CREATE_MODE";
        public static readonly string PRELOAD_MODELS_PLAYER_PREFS_KEY = "IS_PRELOAD_MODELS";

        #region Public Properties
        public string UserId => auth.CurrentUser.UserId;
        public bool HasAnchorLocations => hasAnchorLocations;
        public List<string> QuerriedAnchorIds => querriedLocationAnchorIds;
        public static ModelPayload TranslateModel { get; set; }
        public Dictionary<string, ModelSpawnData> SpawnedObjects => spawnedObjects;
        public bool IsReady { get => isReady; set => isReady = value; }
        #endregion // Public Properties

        // Start is called before the first frame update
        protected virtual void Start()
        {
            if (auth == null)
                auth = Firebase.Auth.FirebaseAuth.DefaultInstance;

            if (statusText != null)
                statusText.gameObject.SetActive(debug);

            isPreloadUserModels = PlayerPrefs.GetInt(MenuUIController.PRELOAD_MODELS_PLAYER_PREFS_KEY, 0) == 1;

            StartCoroutine(InitFirebaseCoroutine());
        }

        protected virtual void Update()
        {
        }

        protected IEnumerator InitFirebaseCoroutine()
        {
            yield return InitFirebaseAsync();
        }

        protected async Task InitFirebaseAsync()
        {
            try
            {
                FirebaseApp.DefaultInstance.SetEditorDatabaseUrl(FIREBASE_URL);

                var dependencyStatus = await Firebase.FirebaseApp.CheckAndFixDependenciesAsync();

                if (dependencyStatus == Firebase.DependencyStatus.Available)
                {
                    await OnFirebaseInitialized();
                    isReady = true;
                }
                else
                {
                    Debug.LogError(System.String.Format("Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                    isReady = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        protected bool IsAuthenticated()
        {
            return auth.CurrentUser != null;
        }

        protected string GetCurrentUserEmail()
        {
            if (IsAuthenticated())
                return auth.CurrentUser.Email;
            return "";
        }

        protected void CreateEmailPassword(string email, string password)
        {
            auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError(task.Exception);
                    SplashText(task.Exception.Message, Color.red);
                    return;
                }

                OnUserCreateSuccessful(task.Result.UserId);
            });
        }

        protected void LoginEmailPassword(string email, string password)
        {
            auth.SignInWithEmailAndPasswordAsync(email, password)
            .ContinueWith(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError("Login encountered an error or was cancelled: " + task.Exception);
                    SplashText(task.Exception.Message, Color.red);
                    return;
                }

                OnUserLoginSuccessful();
            });
        }

        protected void SendPasswordResetEmail(string email)
        {
            auth.SendPasswordResetEmailAsync(email).ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    SplashText(task.Exception.Message, Color.red);
                    Debug.LogError(task.Exception.Message + "\n" + task.Exception.StackTrace);
                    return;
                }

                SplashText("Password Reset Email Sent", Color.green);
            });
        }

        protected Task SendVerificationEmail()
        {
            if (!IsAuthenticated())
            {
                Debug.LogError("Not authenticated");
                return Task.CompletedTask;
            }

            return auth.CurrentUser.SendEmailVerificationAsync();
        }

        protected virtual void OnUserCreateSuccessful(string userId) { }

        protected virtual void OnUserLoginSuccessful() { }

        protected virtual Task OnFirebaseInitialized() { return Task.CompletedTask; }

        public async Task<Dictionary<string, LocationPayload>> FetchUserLocationAnchorIdsAsync(string userId)
        {
            Dictionary<string, LocationPayload> dict = new Dictionary<string, LocationPayload>();

            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    Debug.LogError("No user selected");
                    return dict;
                }

                DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.GetReference("users" + "/" + userId + "/locations").GetValueAsync();

                querriedLocationAnchorIds.Clear();

                if (snapshot.Exists && snapshot.HasChildren)
                {
                    foreach (DataSnapshot location in snapshot.Children)
                    {
                        string locationId = location.Key.ToString();
                        querriedLocationAnchorIds.Add(locationId);

                        string locKey0 = "locationName";
                        string locKey1 = "locationDesc";
                        if (location.HasChild(locKey0) && location.HasChild(locKey1))
                        {
                            dict.Add(locationId, new LocationPayload(
                                location.Child(locKey0).Value.ToString(), location.Child(locKey1).Value.ToString()));
                        }
                        else if (location.HasChild(locKey0))
                        {
                            dict.Add(locationId, new LocationPayload(
                                location.Child(locKey0).Value.ToString(), ""));
                        }
                        else
                        {
                            dict.Add(locationId, new LocationPayload(location.Key, ""));
                            Debug.LogWarning("No location info found... Using location ID instead");
                        }
                    }
                    hasAnchorLocations = true;
                }
                else
                {
                    Debug.LogWarning("No locations found for user - " + userId);
                    hasAnchorLocations = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                SplashText(ex.Message, Color.red);
            }
            return dict;
        }

        public void SplashText(string text, Color color)
        {
            if (splashText != null)
            {
                UnityDispatcher.InvokeOnAppThread(() =>
                {
                    StopCoroutine("SplashTextCoroutine");
                    splashBackground = splashText.GetComponentInChildren<Image>();
                    StartCoroutine(SplashTextCoroutine(text, color));
                });
            }
        }

        private Image splashBackground;

        private IEnumerator SplashTextCoroutine(string text, Color color)
        {
            splashText.text = text;
            splashText.color = color;
            splashText.CrossFadeAlpha(1f, 0f, false);
            splashBackground.CrossFadeAlpha(0.5f, 0f, false);

            splashText.gameObject.SetActive(true);
            splashText.CrossFadeAlpha(0f, 10f, false);
            splashBackground.CrossFadeAlpha(0f, 10f, false);

            yield return new WaitWhile(() => splashText.color.a > 0f || splashBackground.color.a > 0f);

            splashText.gameObject.SetActive(false);
            yield return null;
        }

        /// <summary>
        /// Preloads all models for selectedUserId.
        /// </summary>
        protected virtual void PreLoadAllModelsForUser() { }

        protected Dictionary<string, object> GetFullPayload(LocationPayload lp, ModelPayload mp)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["locationName"] = lp.name;
            result["locationDesc"] = lp.description;
            result["fileName"] = mp.fileName;
            result["filePath"] = mp.filePath;
            result["fileDesc"] = mp.fileDesc;

            if (mp.HasMyFileData())
            {
                var dict = mp.ToDictionary();
                result["myFilePath"] = dict["myFilePath"];
                result["myFileName"] = dict["myFileName"];
                result["myFileDesc"] = dict["myFileDesc"];
            }

            return result;
        }

        public class LocationPayload
        {
            public string name;
            public string description;

            public LocationPayload(string name, string description)
            {
                this.name = name;
                this.description = description;
            }

            public Dictionary<string, object> ToDictionary()
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["locationName"] = name;
                result["locationDesc"] = description;

                return result;
            }

            public override string ToString()
            {
                return name + (!string.IsNullOrEmpty(description) ? " - " + description : "");
            }
        }

        public class ModelPayload
        {
            public string fileName;
            public string filePath;
            public string fileDesc;

            private string myFilePath;
            private string myFileName;
            private string myFileDesc;

            public ModelPayload(string fileName, string filePath, string fileDesc)
            {
                this.fileName = fileName;
                this.filePath = filePath;
                this.fileDesc = fileDesc;
            }

            public void SetMyFileData(string myFileName, string myFilePath, string myFileDesc)
            {
                this.myFileName = myFileName;
                this.myFilePath = myFilePath;
                this.myFileDesc = myFileDesc;
            }

            public override string ToString()
            {
                return fileName.Split('.')[0] + (string.IsNullOrEmpty(fileDesc) ? "" : " - " + fileDesc);
            }

            public Dictionary<string, object> ToDictionary()
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["fileName"] = fileName;
                result["filePath"] = filePath;
                result["fileDesc"] = fileDesc;

                if (HasMyFileData())
                {
                    result["myFilePath"] = myFilePath;
                    result["myFileName"] = myFileName;
                    result["myFileDesc"] = myFileDesc;
                }

                return result;
            }

            public bool HasMyFileData()
            {
                return !string.IsNullOrEmpty(myFilePath);
            }
        }
    }
}
