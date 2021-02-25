using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase.Database;
using System.Threading.Tasks;
using UnityEngine.Events;
using System;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class SetModelUIController : FirebaseManager
    {
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Dropdown locationDropdown;
        [SerializeField] private Button urlButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private InputField urlTextInput;
        [SerializeField] private Sprite usernameSprite;
        [SerializeField] private Sprite otherSprite;

        // Key - userID of model owner
        private Dictionary<string, List<ModelPayload>> modelDict = new Dictionary<string, List<ModelPayload>>();
        // Key - locationID
        private Dictionary<string, LocationPayload> locationDict = new Dictionary<string, LocationPayload>();
        private List<string> resUsernames = new List<string>();



        // Start is called before the first frame update
        protected override void Start()
        {
            SetMainButtonAction(true);
            modelDropdown.options.Insert(0, new Dropdown.OptionData("Loading..."));
            locationDropdown.options.Insert(0, new Dropdown.OptionData("Loading..."));

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

        protected override async Task OnFirebaseInitialized()
        {
            try
            {
                Handheld.StopActivityIndicator();

                locationDict = await FetchUserLocationAnchorIdsAsync(UserId, true);
                string newLocId = PlayerPrefs.GetString(NEW_LOCATIONID_PP_KEY, "");

                foreach (var entry in locationDict)
                {
                    locationDropdown.options.Add(new Dropdown.OptionData(entry.Value.ToString(), otherSprite));

                    if (!string.IsNullOrEmpty(newLocId) && newLocId == entry.Key)
                        locationDropdown.SetValueWithoutNotify(locationDropdown.options.Count - 1);
                }

                if (locationDropdown.options.Count == 2)
                    locationDropdown.SetValueWithoutNotify(1);

                locationDropdown.options[0].text = "";
                locationDropdown.Hide();

                modelDict = await GetAllModelInfoAsync();
                resUsernames.Clear();

                foreach (KeyValuePair<string, List<ModelPayload>> entry in modelDict)
                {
                    List<ModelPayload> models = entry.Value;
                    for (int i = 0; i < models.Count; i++)
                    {
                        bool isUser = entry.Key == UserId;

                        // Username header if start of new list
                        if (i == 0)
                        {
                            DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.GetReference("users/" + entry.Key + "/username").GetValueAsync();
                            string username = snapshot.Value.ToString();
                            resUsernames.Add(username);
                            modelDropdown.options.Insert(isUser ? 1 : modelDropdown.options.Count, new Dropdown.OptionData(username, usernameSprite));
                        }

                        ModelPayload m = models[i];
                        modelDropdown.options.Insert(isUser ? 2 : modelDropdown.options.Count, new Dropdown.OptionData(m.ToString(), otherSprite));
                    }
                }

                modelDropdown.options[0].text = "";
                modelDropdown.Hide();

                OnLocationDropdownChanged();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                SplashText(ex.Message, Color.red);
            }
        }

        /*private int GetLocationDropdownIndex(string locationId)
        {
            LocationPayload loc = locationDict[locationId];
            
            if (loc != null && locationDropdown.options.Count > 0)
            {
                return locationDropdown.options.FindIndex(o => o.text == loc.ToString());
            }

            return 0;
        }*/

        public void OnSaveButtonPressed()
        {
            string selectedLocationId = "";
            ModelPayload selectedModel = null;
            string locationOption = locationDropdown.options[locationDropdown.value].text;
            string modelOption = modelDropdown.options[modelDropdown.value].text;


            foreach (var entry in locationDict)
            {
                if (locationOption == entry.Value.ToString())
                {
                    selectedLocationId = entry.Key;
                    break;
                }
            }

            foreach (List<ModelPayload> userModels in modelDict.Values)
            {
                foreach (ModelPayload m in userModels)
                {
                    if (modelOption == m.ToString())
                    {
                        selectedModel = m;
                        break;
                    }
                }

                if (selectedModel != null)
                    break;
            }

            if (string.IsNullOrEmpty(selectedLocationId))
            {
                SplashText("Select a Location", Color.red);
            }
            else if (selectedModel == null)
            {
                SplashText("Select a 3D Model", Color.red);
            }
            else
            {
                if (SpawnedObjectsSingleton.Instance != null)
                    UnityDispatcher.InvokeOnAppThread(() => SpawnedObjectsSingleton.Instance.RemoveSpawnedObject(selectedLocationId));

                SetModelToLocation(selectedLocationId, selectedModel);
            }
        }

        public void OnURLButtonPressed(string url)
        {
            urlButton.gameObject.SetActive(false);
            urlTextInput.gameObject.SetActive(true);
            urlTextInput.text = url;

            StopCoroutine("OpenURLCoroutine");
            StartCoroutine(OpenURLCoroutine(url));
        }

        private IEnumerator OpenURLCoroutine(string url)
        {
            SetMainButtonAction(false);
            SplashText("For The Best Experience - Open On PC/MAC", Color.white);
            yield return new WaitForSeconds(2.5f);
            Application.OpenURL(url);
            yield return null;
        }

        public void OnBackButtonPressed()
        {
            if (SceneManager.GetActiveScene().name != "AzureSpatialAnchor")
            {
                PlayerPrefs.SetInt(IS_CREATE_MODE_PP_KEY, 0);
                PlayerPrefs.Save();
                SceneManager.LoadSceneAsync("AzureSpatialAnchor");
            }
        }

        private async Task<Dictionary<string, List<ModelPayload>>> GetAllModelInfoAsync()
        {
            string myId = base.UserId;
            Dictionary<string, List<ModelPayload>> dict = new Dictionary<string, List<ModelPayload>>();
            DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.GetReference("users").GetValueAsync();

            foreach (DataSnapshot user in snapshot.Children)
            {
                if (user.HasChild("locations"))
                {
                    foreach (DataSnapshot location in user.Child("locations").Children)
                    {
                        if (location.HasChild("filePath"))
                        {
                            List<string> paths = new List<string>() { "filePath" };
                            if (location.HasChild("myFilePath"))
                                paths.Add("myFilePath");

                            foreach (string fp in paths)
                            {
                                string filePath = location.Child(fp).Value.ToString();
                                string[] pp = filePath.Split('/');

                                if (pp.Length > 1 &&
                                    (pp[0] == myId || pp[1] == "public"))
                                {
                                    string userKey = pp[0];
                                    string fileName = location.Child((fp == "myFilePath" ? "myFileName" : "fileName")).Value.ToString();
                                    string fileDesc = location.Child((fp == "myFilePath" ? "myFileDesc" : "fileDesc")).Value.ToString();
                                    ModelPayload modelPayload = new ModelPayload(fileName, filePath, fileDesc);

                                    if (dict.ContainsKey(userKey))
                                    {
                                        if (dict[userKey].Exists(m => m.filePath == filePath))
                                            continue;

                                        dict[userKey].Add(modelPayload);
                                    }
                                    else
                                    {
                                        dict.Add(userKey, new List<ModelPayload>() { modelPayload });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return dict;
        }

        public void OnModelDropdownValueChanged()
        {
            int index = modelDropdown.value;
            var options = modelDropdown.options;

            if (resUsernames.Contains(options[index].text)
                && (index + 1) < options.Count)
            {
                modelDropdown.value = index + 1;
                modelDropdown.RefreshShownValue();
            }

            SetMainButtonAction(true);
        }

        public void OnLocationDropdownChanged()
        {
            SetMainButtonAction(true);

            string locSelection = locationDropdown.options[locationDropdown.value].text;

            if (!string.IsNullOrEmpty(locSelection))
            {
                foreach (var loc in locationDict)
                {
                    if (loc.Value.ToString() == locSelection)
                    {
                        if (loc.Value.HasModel)
                        {
                            string linkedModel = loc.Value.ModelPayload.ToString();
                            int mIndex = modelDropdown.options.FindIndex(m => m.text == linkedModel);
                            modelDropdown.SetValueWithoutNotify(mIndex > 0 ? mIndex : 0);
                        }
                        else
                        {
                            modelDropdown.SetValueWithoutNotify(0);
                        }
                        break;
                    }
                }
            }
            else
            {
                modelDropdown.SetValueWithoutNotify(0);
            }
        }

        private async void SetModelToLocation(string locationId, ModelPayload modelPayload)
        {
            try
            {
                DataSnapshot snapshot = await FirebaseDatabase.DefaultInstance.GetReference("users" + "/" + base.UserId + "/locations/" + locationId).GetValueAsync();


                if (snapshot.HasChild("filePath"))
                {
                    string myFilePath = snapshot.Child("filePath").Value.ToString();
                    if (myFilePath.Split('/')[0] == UserId)
                    {
                        string myFileName = snapshot.Child("fileName").Value.ToString();
                        string myFileDesc = snapshot.Child("fileDesc").Value.ToString();

                        modelPayload.SetMyFileData(myFileName, myFilePath, myFileDesc);
                    }
                }

                await FirebaseDatabase.DefaultInstance.GetReference("users" + "/" + base.UserId + "/locations/" + locationId)
                    .UpdateChildrenAsync(modelPayload.ToDictionary());

                SplashText(modelPayload.fileName + " -> " + locationDict[locationId].name, Color.green);
                SetMainButtonAction(false);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                SplashText(ex.Message, Color.red);
            }
        }

        private Color initialButtonColor = Color.clear;

        private void SetMainButtonAction(bool isSave)
        {
            UnityDispatcher.InvokeOnAppThread(() =>
            {
                saveButton.onClick.RemoveAllListeners();
                Text buttonText = saveButton.GetComponentInChildren<Text>();
                Lean.Gui.LeanBox buttonImage = saveButton.GetComponent<Lean.Gui.LeanBox>();
                if (initialButtonColor == Color.clear)
                    initialButtonColor = buttonImage.color;

                if (isSave)
                {
                    saveButton.onClick.AddListener(OnSaveButtonPressed);
                    buttonImage.color = initialButtonColor;
                    buttonText.text = "SAVE";
                }
                else
                {
                    saveButton.onClick.AddListener(OnBackButtonPressed);
                    buttonImage.color = new Color32(0, 120, 254, 200);
                    buttonText.text = "DONE";
                }
            });
        }
    }
}