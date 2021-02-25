using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Firebase.Database;
using System.Threading.Tasks;
using System;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class SetModelUIController : FirebaseManager
    {
        [SerializeField] private Dropdown modelDropdown;
        [SerializeField] private Dropdown locationDropdown;
        [SerializeField] private Button urlButton;
        [SerializeField] private InputField urlTextInput;
        [SerializeField] private Sprite usernameSprite;
        [SerializeField] private Sprite otherSprite;

        private Dictionary<string, List<ModelPayload>> modelDict = new Dictionary<string, List<ModelPayload>>();
        private Dictionary<string, LocationPayload> locationDict = new Dictionary<string, LocationPayload>();
        private List<string> resUsernames = new List<string>();


        // Start is called before the first frame update
        protected override void Start()
        {
            modelDropdown.options.Insert(0, new Dropdown.OptionData(""));
            locationDropdown.options.Insert(0, new Dropdown.OptionData(""));
            base.Start();
        }

        protected override async Task OnFirebaseInitialized()
        {
            try
            {
                locationDict = await FetchUserLocationAnchorIdsAsync(UserId);

                foreach (LocationPayload loc in locationDict.Values)
                {
                    locationDropdown.options.Add(new Dropdown.OptionData(loc.ToString(), otherSprite));
                }

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
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                SplashText(ex.Message, Color.red);
            }
        }

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
            SplashText("Open On PC/MAC To Upload Models", Color.yellow);
            yield return new WaitForSeconds(3f);
            Application.OpenURL(url);
            yield return null;
        }

        public void OnBackButtonPressed()
        {
            SceneManager.LoadSceneAsync("Menu UI", LoadSceneMode.Single);
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

                SplashText(modelPayload.fileName + " Added To " + locationDict[locationId].name, Color.green);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                SplashText(ex.Message, Color.red);
            }
        }
    }
}