using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase.Database;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class CreateUIController : FirebaseManager
    {
        [SerializeField]
        private InputField usernameInput;
        [SerializeField]
        private InputField emailInput;
        [SerializeField]
        private InputField passwordInput;

        private HashSet<string> usernames = new HashSet<string>();


        // Start is called before the first frame update
        protected override void Start()
        {
            base.Start();
        }

        protected override void Update()
        {
            base.Update();

            if (Application.platform == RuntimePlatform.Android)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    OnBackButtonPressed();
                }
            }
        }

        protected override Task OnFirebaseInitialized()
        {
            FirebaseDatabase.DefaultInstance.GetReference("users").ChildAdded += OnUserAdded;
            return Task.CompletedTask;
        }

        private void OnUserAdded(object sender, ChildChangedEventArgs e)
        {
            if (e.Snapshot.Exists && e.Snapshot.HasChild("username"))
            {
                string u = e.Snapshot.Child("username").Value.ToString();

                if (usernames.Contains(u) == false)
                {
                    usernames.Add(u);
                }
            }
        }

        public void OnBackButtonPressed()
        {
            Debug.Log("Returning to login screen");
            SceneManager.LoadSceneAsync("Login UI", LoadSceneMode.Single);
        }

        public void OnEmailEndEdit()
        {
            string email = emailInput.text;

            if (!string.IsNullOrEmpty(email) && email.Contains("@"))
            {
                usernameInput.text = email.Split('@')[0];
            }
        }

        public void OnCreateButtonPressed()
        {
            if (string.IsNullOrEmpty(usernameInput.text)
                || string.IsNullOrEmpty(emailInput.text)
                || string.IsNullOrEmpty(passwordInput.text))
                return;

            if (usernames.Contains(usernameInput.text))
            {
                SplashText("That Username Is Taken", Color.red);
                return;
            }

            if (passwordInput.text.Length >= 6)
            {
                Debug.Log("Creating account");
                SplashText("Creating Account...", Color.white);
                CreateEmailPassword(emailInput.text, passwordInput.text);
            }
            else
            {
                SplashText("Password Must Be Atleast 6 Characters Long", Color.red);
            }
        }

        protected override async void OnUserCreateSuccessful(string userId)
        {
            try
            {
                await WriteUserInfoFirebase(userId);
                await SendVerificationEmail();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                Debug.Log("Account Created");
                PlayerPrefs.SetString(LOGIN_EMAIL_PP_KEY, emailInput.text);
                PlayerPrefs.SetString(LOGIN_PASSWORD_PP_KEY, passwordInput.text);
                PlayerPrefs.Save();

                if (!IsAuthenticated)
                    SceneManager.LoadSceneAsync("Login UI");
                else
                    SceneManager.LoadSceneAsync("Menu UI");
            });
        }

        private Task WriteUserInfoFirebase(string userId)
        {
            Dictionary<string, object> childUpdates = new Dictionary<string, object>();
            childUpdates[userId] = new UserPayload(usernameInput.text, GetCurrentUserEmail()).ToDictionary();

            return FirebaseDatabase.DefaultInstance.GetReference("users").UpdateChildrenAsync(childUpdates);
        }

        private class UserPayload
        {
            private string username;
            private string email;

            public UserPayload(string username, string email)
            {
                this.username = username;
                this.email = email;
            }

            public Dictionary<string, object> ToDictionary()
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["username"] = username;
                result["email"] = email;

                return result;
            }
        }
    }
}
