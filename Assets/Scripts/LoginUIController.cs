using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class LoginUIController : FirebaseManager
    {
        [SerializeField]
        private InputField emailInput;
        [SerializeField]
        private InputField passwordInput;


        protected override Task OnFirebaseInitialized()
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                if (IsAuthenticated && PlayerPrefs.HasKey(LOGIN_PASSWORD_PP_KEY))
                {
                    SceneManager.LoadSceneAsync("Menu UI");
                }
                else
                {
                    emailInput.SetTextWithoutNotify(PlayerPrefs.GetString(LOGIN_EMAIL_PP_KEY, ""));
                    passwordInput.SetTextWithoutNotify(PlayerPrefs.GetString(LOGIN_PASSWORD_PP_KEY, ""));
                    OnLoginButtonPressed();
                }
            });

            return Task.CompletedTask;
        }

        // CreateAccount button UnityEvent
        public void OnCreateAccountButtonPressed()
        {
            SceneManager.LoadSceneAsync("Create UI", LoadSceneMode.Single);
        }

        // ResetPassword button UnityEvent
        public void OnResetPasswordButtonPressed()
        {
            if (string.IsNullOrEmpty(emailInput.text))
            {
                SplashText("Enter Your Email To Send A Password Reset Email", Color.red);
                return;
            }

            SendPasswordResetEmail(emailInput.text);
        }

        public void OnPasswordEndEdit()
        {
            OnLoginButtonPressed();
        }

        // Login button UnityEvent
        public void OnLoginButtonPressed()
        {
            if (!string.IsNullOrEmpty(emailInput.text) && !string.IsNullOrEmpty(passwordInput.text))
            {
                Debug.Log("Logging in");
                LoginEmailPassword(emailInput.text, passwordInput.text);
            }
        }

        protected override void OnUserLoginSuccessful()
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                Debug.Log("Logged in");
                PlayerPrefs.SetString(LOGIN_EMAIL_PP_KEY, emailInput.text);
                PlayerPrefs.SetString(LOGIN_PASSWORD_PP_KEY, passwordInput.text);
                PlayerPrefs.Save();

                SceneManager.LoadSceneAsync("Menu UI", LoadSceneMode.Single);
            });
        }
    }
}
