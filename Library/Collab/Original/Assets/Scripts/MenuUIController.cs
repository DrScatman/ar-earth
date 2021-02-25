using UnityEngine;
using Firebase.Database;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.EventSystems;
using System.Linq;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class MenuUIController : FirebaseManager
    {
        [SerializeField] private CustomInputField inputField;
        [SerializeField] private Dropdown dropdown;
        [SerializeField] private RectTransform createLocationButton;
        [SerializeField] private RectTransform addModelButton;
        [SerializeField] private Toggle togglePreloadModels;
        [SerializeField] private Sprite image;
        [SerializeField] private Button startButton;
        [Header("Ads Manager")]
        [SerializeField] private AdsManager adsManager;

        private readonly Dictionary<string, Dropdown.OptionData> userMap = new Dictionary<string, Dropdown.OptionData>();
        private string selectedUsername = "";
        private string selectedUserId = "";
        private bool hasSetModel;

        protected override void Start()
        {
            adsManager.SetRewardedVideoButton(startButton, true);
            dropdown.options.Insert(0, new Dropdown.OptionData(""));
            
            base.Start();
        }

        protected override async Task OnFirebaseInitialized()
        {
            if (!IsAuthenticated())
            {
                Debug.LogError("Not Authenticated - Logging Out...");
                OnLogoutButtonPressed();
                return;
            }

            UnityDispatcher.InvokeOnAppThread(() =>
            {
                FirebaseDatabase.DefaultInstance.GetReference("users").ChildAdded += HandleUserIdAdded;
                FetchHasSetModel();
            });

            try
            {
                await FetchUserLocationAnchorIdsAsync(base.UserId);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                SplashText(ex.Message, Color.red);
            }
        }

        private bool isCurrentUser;

        protected override void Update()
        {
            base.Update();

            selectedUsername = inputField.text;
            selectedUserId = GetUserId(selectedUsername);
            isCurrentUser = selectedUserId == UserId;

            createLocationButton.gameObject.SetActive(isCurrentUser);
            addModelButton.gameObject.SetActive(isCurrentUser && hasAnchorLocations);

            if (!addModelButton.gameObject.activeSelf)
                createLocationButton.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 445f);
            else
                createLocationButton.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 215f);

            if ((isCurrentUser && !hasAnchorLocations) || !hasSetModel)
                startButton.interactable = false;
            else if (!startButton.interactable)
                startButton.interactable = adsManager.IsUnityAdsReady();
        }

        // To be invoked when a userId is selected from the Dropdown
        public void OnDropdownEvent()
        {
            selectedUsername = dropdown.options[dropdown.value].text;
            inputField.SetTextWithoutNotify(selectedUsername);

            if (GetUserId(selectedUsername) != UserId)
                SplashText("View This World Or Select Your Username To Create A World", Color.white);
        }

        public void StartAzureSpatialAnchor()
        {
            if (!hasAnchorLocations && selectedUserId == UserId)
            {
                SplashText("First Create A Model Location, Then Add A Model!", Color.red);
                return;
            }

            PlayerPrefs.SetString(SELECTED_USER_ID_PP_KEY, selectedUserId);
            PlayerPrefs.SetInt(PRELOAD_MODELS_PLAYER_PREFS_KEY, togglePreloadModels.isOn ? 1 : 0);
            PlayerPrefs.SetInt(IS_CREATE_MODE_PP_KEY, 0);
            PlayerPrefs.Save();

            SceneManager.LoadScene("AzureSpatialAnchor", LoadSceneMode.Single);
        }

        private string GetUserId(string username)
        {
            foreach (KeyValuePair<string, Dropdown.OptionData> entry in userMap)
            {
                if (entry.Value.text == username)
                {
                    return entry.Key;
                }
            }
            return "";
        }

        public void OnReplaceLocationButtonPressed()
        {
            SceneManager.LoadScene("ReplaceLocation UI", LoadSceneMode.Single);
        }

        public void LoadCreateLocationScene()
        {
            if (!IsAuthenticated())
            {
                OnLogoutButtonPressed();
                return;
            }
            if (selectedUserId != UserId)
            {
                SplashText("Select Your World To Create A Location!", Color.red);
                return;
            }

            PlayerPrefs.SetInt(IS_CREATE_MODE_PP_KEY, 1);
            PlayerPrefs.SetInt(PRELOAD_MODELS_PLAYER_PREFS_KEY, 0);
            PlayerPrefs.SetString(SELECTED_USER_ID_PP_KEY, UserId);
            PlayerPrefs.Save();

            SceneManager.LoadSceneAsync("AzureSpatialAnchor", LoadSceneMode.Single);
        }

        public void OnAddModelButtonEvent()
        {
            if (!IsAuthenticated())
            {
                OnLogoutButtonPressed();
                return;
            }
            if (!hasAnchorLocations)
            {
                SplashText("Create A Model Location First!", Color.red);
                return;
            }
            if (selectedUserId != UserId)
            {
                SplashText("Select Your World To Add Models!", Color.red);
                return;
            }

            SceneManager.LoadSceneAsync("SetModel UI", LoadSceneMode.Single);
        }

        private void HandleUserIdAdded(object sender, ChildChangedEventArgs args)
        {
            if (args.DatabaseError != null)
            {
                Debug.LogError(args.DatabaseError);
                return;
            }

            DataSnapshot snapshot = args.Snapshot;

            if (!userMap.ContainsKey(snapshot.Key))
            {
                string username = snapshot.Child("username").Value.ToString();

                userMap.Add(snapshot.Key, new Dropdown.OptionData(username, image));

                if (UserId == snapshot.Key)
                {
                    inputField.SetTextWithoutNotify(username);
                    dropdown.options.Insert(1, new Dropdown.OptionData(username, image));
                    dropdown.SetValueWithoutNotify(0);
                }
            }
        }

        public void OnInputFieldChanged()
        {
            string currentInput = inputField.text;

            if (string.IsNullOrEmpty(currentInput) || string.IsNullOrWhiteSpace(currentInput))
                return;

            List<Dropdown.OptionData> newOptions = new List<Dropdown.OptionData> { new Dropdown.OptionData("") };
            newOptions.AddRange(ClosestMatching3(currentInput, userMap.Values.ToList()));

            inputField.canDeactivate = false;
            dropdown.ClearOptions();
            dropdown.AddOptions(newOptions);
            dropdown.SetValueWithoutNotify(0);

            RefreshDropdownListUI();

            EventSystem.current.SetSelectedGameObject(inputField.gameObject, null);
            inputField.OnPointerClick(new PointerEventData(EventSystem.current));
            inputField.MoveTextEnd(true);
            inputField.canDeactivate = true;
        }

        private void RefreshDropdownListUI()
        {
            dropdown.gameObject.SetActive(false);
            dropdown.gameObject.SetActive(true);
            dropdown.Show();
        }

        public void OnInputFieldEndEdit(string call)
        {
            //inputField.SetTextWithoutNotify(dropdown.options[dropdown.value].text);
        }

        public void OnLogoutButtonPressed()
        {
            PlayerPrefs.SetString(LOGIN_PASSWORD_PP_KEY, "");
            PlayerPrefs.Save();
            SceneManager.LoadSceneAsync("Login UI", LoadSceneMode.Single);
        }

        public List<Dropdown.OptionData> ClosestMatching3(string compare, List<Dropdown.OptionData> options)
        {
            Dictionary<Dropdown.OptionData, int> matchesDict = new Dictionary<Dropdown.OptionData, int>();
            List<Dropdown.OptionData> top3 = new List<Dropdown.OptionData>();
            compare = compare.ToLower();

            var eqOpt = options.Find(o => o.text.ToLower() == compare);

            if (eqOpt != null)
            {
                matchesDict.Add(eqOpt, -1);
            }

            foreach (var o in options)
            {
                string oText = o.text;

                if (oText.ToLower() != compare)
                {
                    int index = oText.ToLower().IndexOf(compare);

                    if (index >= 0)
                    {
                        matchesDict.Add(o, index);
                    }
                }
            }

            var orderedMatches = matchesDict.OrderBy(t => t.Value).ToList();

            foreach (var m in orderedMatches)
            {
                if (top3.Count >= 3) break;

                top3.Add(m.Key);
            }

            return top3;
        }

        private async void FetchHasSetModel()
        {
            try
            {
                DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.GetReference("users" + "/" + UserId + "/locations").GetValueAsync();

                if (snapshot.Exists && snapshot.HasChildren)
                {
                    foreach (DataSnapshot location in snapshot.Children)
                    {
                        if (location.HasChild("filePath"))
                        {
                            hasSetModel = true;
                            return;
                        }
                    }
                }
            }
            catch (Exception e) { Debug.LogError(e); }

            hasSetModel = false;
        }
    }
}
