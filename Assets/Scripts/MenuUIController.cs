using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class MenuUIController : FirebaseManager
    {
        [SerializeField] private CustomInputField inputField;
        [SerializeField] private Dropdown dropdown;
        [SerializeField] public RectTransform createLocationButton;
        [SerializeField] private RectTransform addModelButton;
        [SerializeField] private Toggle togglePreloadModels;
        [SerializeField] private Sprite image;
        [SerializeField] private Button startButton;
        [SerializeField] private GameObject helpModal;

        // private readonly Dictionary<string, Dropdown.OptionData> userMap = new Dictionary<string, Dropdown.OptionData>();
        // private string selectedUsername = "";
        // private string selectedUserId = "";
        // private bool hasSetModel;
        private static bool isShowAds;


        protected override void Start()
        {
            StartCoroutine(LocationHelper.DetectCountry());

            if (isShowAds)
            {
                AdsManager.Instance.SetVideoAdButton(startButton, StartAzureSpatialAnchor, true);
            }
            else
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(StartAzureSpatialAnchor);
            }

            dropdown.options.Insert(0, new Dropdown.OptionData(""));

            base.Start();
        }

        protected override Task OnFirebaseInitialized()
        {
            if (!IsAuthenticated)
            {
                Debug.LogError("Not Authenticated - Logging Out...");
                OnLogoutButtonPressed();
            }

            return Task.CompletedTask;
            // UnityDispatcher.InvokeOnAppThread(() =>
            // {
            //     FirebaseDatabase.DefaultInstance.GetReference("users").ChildAdded += HandleUserIdAdded;
            // });

            // try
            // {
            //     await FetchUserLocationAnchorIdsAsync(base.UserId);
            // }
            // catch (Exception ex)
            // {
            //     Debug.LogError(ex);
            //     SplashText(ex.Message, Color.red);
            //     throw ex;
            // }
        }

        private bool isCurrentUser;

        protected override void Update()
        {
            base.Update();

            // selectedUsername = inputField.text;
            // selectedUserId = GetUserId(selectedUsername);
            // isCurrentUser = selectedUserId == UserId;

            // createLocationButton.gameObject.SetActive(isCurrentUser);
            // addModelButton.gameObject.SetActive(isCurrentUser && HasAnchorLocations);

            // if (!addModelButton.gameObject.activeSelf)
            //     createLocationButton.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 445f);
            // else
            //     createLocationButton.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 215f);

            // if (!createLocationButton.gameObject.activeSelf && !addModelButton.gameObject.activeSelf)
            //     createLocationButton.transform.parent.gameObject.SetActive(false);
            // else
            //     createLocationButton.transform.parent.gameObject.SetActive(true);

            startButton.interactable = CanStart();

            if (Application.platform == RuntimePlatform.Android)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    OnLogoutButtonPressed();
                }
            }
        }

        private bool CanStart()
        {
            return IsAuthenticated && (!isShowAds || AdsManager.Instance.IsReady) && LocationHelper.isDetected;
        }

        // To be invoked when a userId is selected from the Dropdown
        // public void OnDropdownEvent()
        // {
        //     selectedUsername = dropdown.options[dropdown.value].text;
        //     inputField.SetTextWithoutNotify(selectedUsername);

        //     if (GetUserId(selectedUsername) != UserId)
        //         SplashText("View This World Or Select Your Username To Create A World", Color.white);
        // }

        public void StartAzureSpatialAnchor()
        {
            // if (!HasAnchorLocations && selectedUserId == UserId)
            // {
            //     SplashText("First Create A Model Location, Then Add A Model!", Color.red);
            //     return;
            // }

            PlayerPrefs.SetInt(IS_CREATE_MODE_PP_KEY, 0);
            PlayerPrefs.Save();

            isShowAds = false;
            SceneManager.LoadSceneAsync("AzureSpatialAnchor", LoadSceneMode.Single);
        }

        // private string GetUserId(string username)
        // {
        //     foreach (KeyValuePair<string, Dropdown.OptionData> entry in userMap)
        //     {
        //         if (entry.Value.text == username)
        //         {
        //             return entry.Key;
        //         }
        //     }
        //     return "";
        // }

        public void OnReplaceLocationButtonPressed()
        {
            SceneManager.LoadScene("ReplaceLocation UI", LoadSceneMode.Single);
        }

        public void LoadCreateLocationScene()
        {
            if (!IsAuthenticated)
            {
                OnLogoutButtonPressed();
                return;
            }
            // if (selectedUserId != UserId)
            // {
            //     SplashText("Select Your World To Create A Location!", Color.red);
            //     return;
            // }

            PlayerPrefs.SetInt(IS_CREATE_MODE_PP_KEY, 1);
            PlayerPrefs.Save();

            SceneManager.LoadSceneAsync("AzureSpatialAnchor", LoadSceneMode.Single);
        }

        public void OnAddModelButtonEvent()
        {
            if (!IsAuthenticated)
            {
                OnLogoutButtonPressed();
                return;
            }


            SceneManager.LoadSceneAsync("SetModel UI", LoadSceneMode.Single);
        }

        // private void HandleUserIdAdded(object sender, ChildChangedEventArgs args)
        // {
        //     if (args.DatabaseError != null)
        //     {
        //         Debug.LogError(args.DatabaseError);
        //         return;
        //     }

        //     DataSnapshot snapshot = args.Snapshot;

        //     if (snapshot.Exists && !userMap.ContainsKey(snapshot.Key))
        //     {
        //         bool hasModel = false;

        //         if (snapshot.HasChild("locations") && snapshot.Child("locations").HasChildren)
        //         {
        //             foreach (var loc in snapshot.Child("locations").Children)
        //             {
        //                 if (loc.HasChild("filePath"))
        //                 {
        //                     hasModel = true;
        //                     break;
        //                 }
        //             }
        //         }

        //         if (UserId == snapshot.Key)
        //         {
        //             string username = snapshot.Child("username").Value.ToString();
        //             userMap.Add(snapshot.Key, new Dropdown.OptionData(username, image));

        //             inputField.SetTextWithoutNotify(username);
        //             dropdown.options.Insert(1, new Dropdown.OptionData(username, image));
        //             dropdown.SetValueWithoutNotify(0);
        //             hasSetModel = hasModel;
        //         }
        //         else if (hasModel)
        //         {
        //             string username = snapshot.Child("username").Value.ToString();
        //             userMap.Add(snapshot.Key, new Dropdown.OptionData(username, image));
        //         }
        //     }
        // }

        // public void OnInputFieldChanged()
        // {
        //     string currentInput = inputField.text;

        //     if (string.IsNullOrEmpty(currentInput) || string.IsNullOrWhiteSpace(currentInput))
        //         return;

        //     List<Dropdown.OptionData> newOptions = new List<Dropdown.OptionData> { new Dropdown.OptionData("") };
        //     newOptions.AddRange(ClosestMatching3(currentInput, userMap.Values.ToList()));

        //     inputField.canDeactivate = false;
        //     dropdown.ClearOptions();
        //     dropdown.AddOptions(newOptions);
        //     dropdown.SetValueWithoutNotify(0);

        //     RefreshDropdownListUI();

        //     EventSystem.current.SetSelectedGameObject(inputField.gameObject, null);
        //     inputField.OnPointerClick(new PointerEventData(EventSystem.current));
        //     inputField.MoveTextEnd(true);
        //     inputField.canDeactivate = true;
        // }

        // private void RefreshDropdownListUI()
        // {
        //     dropdown.gameObject.SetActive(false);
        //     dropdown.gameObject.SetActive(true);
        //     dropdown.Show();
        // }

        // public void OnInputFieldEndEdit(string call)
        // {
        //     //inputField.SetTextWithoutNotify(dropdown.options[dropdown.value].text);
        // }

        public void OnLogoutButtonPressed()
        {
            PlayerPrefs.DeleteKey(LOGIN_PASSWORD_PP_KEY);
            PlayerPrefs.Save();
            SignOut();
        }

        // public List<Dropdown.OptionData> ClosestMatching3(string compare, List<Dropdown.OptionData> options)
        // {
        //     Dictionary<Dropdown.OptionData, int> matchesDict = new Dictionary<Dropdown.OptionData, int>();
        //     List<Dropdown.OptionData> top3 = new List<Dropdown.OptionData>();
        //     compare = compare.ToLower();

        //     var eqOpt = options.Find(o => o.text.ToLower() == compare);

        //     if (eqOpt != null)
        //     {
        //         matchesDict.Add(eqOpt, -1);
        //     }

        //     foreach (var o in options)
        //     {
        //         string oText = o.text;

        //         if (oText.ToLower() != compare)
        //         {
        //             int index = oText.ToLower().IndexOf(compare);

        //             if (index >= 0)
        //             {
        //                 matchesDict.Add(o, index);
        //             }
        //         }
        //     }

        //     var orderedMatches = matchesDict.OrderBy(t => t.Value).ToList();

        //     foreach (var m in orderedMatches)
        //     {
        //         if (top3.Count >= 3) break;

        //         top3.Add(m.Key);
        //     }

        //     return top3;
        // }

        public void ToggleHelpModal(bool isActive)
        {
            helpModal.SetActive(isActive);
        }

        public void OpenURL(string url)
        {
            Application.OpenURL(url);
        }
    }
}
