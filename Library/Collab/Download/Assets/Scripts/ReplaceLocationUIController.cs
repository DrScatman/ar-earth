using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Firebase.Database;
using System;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class ReplaceLocationUIController : FirebaseManager
    {
        [SerializeField] private Dropdown locationDropdown;
        [SerializeField] private Sprite dropdownItemSprite;
        [SerializeField] private Button replaceButton;
        [Header("Ads Manager")]
        [SerializeField] private AdsManager adsManager;

        private Dictionary<string, LocationPayload> locationDict = new Dictionary<string, LocationPayload>();
        private string previousFilepath = "";

        // Start is called before the first frame update
        protected override void Start()
        {
            adsManager.SetRewardedVideoButton(replaceButton, false);
            locationDropdown.options.Insert(0, new Dropdown.OptionData(""));
            base.Start();
        }

        protected override async Task OnFirebaseInitialized()
        {
            locationDict = await FetchUserLocationAnchorIdsAsync(UserId);

            foreach (LocationPayload loc in locationDict.Values)
            {
                locationDropdown.options.Add(new Dropdown.OptionData(loc.name + " - " + loc.description, dropdownItemSprite));
            }
        }

        public void OnBackButtonPressed()
        {
            SceneManager.LoadSceneAsync("Menu UI", LoadSceneMode.Single);
        }

        public void OnDropdownValueChanged()
        {
            SplashText("Warning:  This Will Delete The Selected Location!", Color.yellow);
        }

        public async void ReplaceLocation()
        {
            string locationItem = locationDropdown.options[locationDropdown.value].text;

            if (!string.IsNullOrEmpty(locationItem))
            {
                foreach (var entry in locationDict)
                {
                    LocationPayload payload = entry.Value;

                    if (locationItem.Contains(payload.name) &&
                        (string.IsNullOrEmpty(payload.description) || locationItem.Contains(payload.description)))
                    {
                        try
                        {
                            await DeleteLocationAsync(entry.Key);
                            UnityDispatcher.InvokeOnAppThread(() =>
                            {
                                // PlayerPrefs.DeleteKey(IapManager.TOKEN_CONSUMED_DEVICE_LOCK_KEY);
                                LoadCreateLocationScene();
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex);
                            SplashText(ex.Message, Color.red);
                        }
                    }
                }
            }
            else
            {
                SplashText("Select A Location To Replace!", Color.red);
            }
        }

        private async Task DeleteLocationAsync(string locationId)
        {
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.GetReference("users/" + UserId + "/locations/" + locationId);

            if (reference.Child("filePath") != null)
            {
                DataSnapshot s = await reference.Child("filePath").GetValueAsync();

                if (s.Exists)
                {
                    previousFilepath = s.Value.ToString();
                }
            }

            await reference.RemoveValueAsync();
        }

        private void LoadCreateLocationScene()
        {
            if (!string.IsNullOrEmpty(previousFilepath))
                PlayerPrefs.SetString(TRANSLATE_FILEPATH_PP_KEY, previousFilepath);
            PlayerPrefs.SetInt(IS_CREATE_MODE_PP_KEY, 1);
            PlayerPrefs.SetInt(PRELOAD_MODELS_PLAYER_PREFS_KEY, 0);
            PlayerPrefs.SetString(SELECTED_USER_ID_PP_KEY, UserId);
            PlayerPrefs.Save();

            SceneManager.LoadSceneAsync("AzureSpatialAnchor", LoadSceneMode.Single);
        }
    }
}