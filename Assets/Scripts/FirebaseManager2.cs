using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading.Tasks;

using Firebase;
using Firebase.Database;
using Firebase.Unity.Editor;
using Firebase.Extensions;

namespace Assets.Scripts
{
    public class FirebaseManager2 : MonoBehaviour
    {
        protected readonly string FIREBASE_URL = "https://XXXX.firebaseio.com/";
        private Firebase.Auth.FirebaseAuth auth;
        private bool isReady = false;


        protected async void InitFirebase()
        {
            try
            {
                isReady = false;
                Firebase.DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();

                if (dependencyStatus == DependencyStatus.Available)
                {
                    auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
                    FirebaseApp.DefaultInstance.SetEditorDatabaseUrl(FIREBASE_URL);

                    await OnFirebaseInitialized();
                    isReady = true;
                }
                else
                {
                    Debug.LogError(string.Format("Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.GetBaseException());
            }
        }

        protected virtual Task OnFirebaseInitialized() { return Task.CompletedTask; }

        protected void LoginEmailPassword(string email, string password)
        {
            auth.SignInWithEmailAndPasswordAsync(email, password)
            .ContinueWith(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError("Login encountered an error or was cancelled:\t" + task.Exception);
                    return;
                }

                OnUserLoginSuccessful();
            });
        }


        protected void SignOut()
        {
            Debug.Log("Signing out.");
            auth.SignOut();
            if (SceneManager.GetActiveScene().name != "Login UI") SceneManager.LoadScene("Login UI");
        }

        protected string GetCurrentUserEmail()
        {
            if (IsAuthenticated)
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
                    Debug.LogError("Login encountered an error or was cancelled:\t" + task.Exception);
                    SplashText(task.Exception.GetBaseException().Message, Color.red);
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
                    SplashText(task.Exception.GetBaseException().Message, Color.red);
                    Debug.LogError(task.Exception);
                    return;
                }

                SplashText("Password Reset Email Sent", Color.green);
            });
        }

        protected Task SendVerificationEmail()
        {
            if (!IsAuthenticated)
            {
                Debug.LogError("Not authenticated");
                return Task.CompletedTask;
            }

            return auth.CurrentUser.SendEmailVerificationAsync();
        }

        protected virtual void OnUserCreateSuccessful(string userId) { }

        protected virtual void OnUserLoginSuccessful() { }

        private Dictionary<string, ModelPayload> fetchedLocationsDict = new Dictionary<string, ModelPayload>();

        protected virtual async void FetchUserLocationAnchorIdsAsync(string userId, bool setLinkedModels = false)
        {
            try
            {
                DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance
                                                .GetReference("users" + "/" + userId + "/locations")
                                                .GetValueAsync();

                if (snapshot.Exists && snapshot.HasChildren)
                {
                    foreach (DataSnapshot child in snapshot.Children)
                    {
                        string childId = child.Key.ToString();
                        fetchedLocationsDict[childId] = new ModelPayload(
                                    child.Child("fileName").Value.ToString(),
                                    child.Child("filePath").Value.ToString(),
                                    child.Child("fileDesc").Value.ToString()
                                );
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        public void SplashText(string text, Color color)
        {
            if (splashTextObj != null)
            {
                UnityDispatcher.InvokeOnAppThread(() =>
                {
                    StopCoroutine("SplashTextCoroutine");
                    Text splashText = splashTextObj.GetComponentInChildren<Text>();
                    Lean.Gui.LeanBox splashImage = splashTextObj.GetComponentInChildren<Lean.Gui.LeanBox>();
                    StartCoroutine(SplashTextCoroutine(splashText, splashImage, text, color));
                });
            }
        }

        private IEnumerator SplashTextCoroutine(Text splashText, Lean.Gui.LeanBox splashImage, string text, Color color)
        {
            splashText.text = text;
            splashText.color = color;
            splashText.CrossFadeAlpha(1f, 0f, false);
            splashImage.CrossFadeAlpha(0.66667f, 0f, false);

            splashTextObj.gameObject.SetActive(true);
            splashText.CrossFadeAlpha(0f, 16f, false);
            splashImage.CrossFadeAlpha(0f, 16f, false);

            yield return new WaitWhile(() => splashText.color.a > 0f || splashImage.color.a > 0f);

            splashTextObj.gameObject.SetActive(false);
            yield return null;
        }

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
            public bool HasModel { get; set; }
            public ModelPayload ModelPayload { get; set; }

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
