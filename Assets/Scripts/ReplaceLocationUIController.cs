using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Firebase.Database;
using System;
using UnityEngine.Purchasing;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class ReplaceLocationUIController : FirebaseManager
    {
        [SerializeField] private Dropdown locationDropdown;
        [SerializeField] private Sprite dropdownItemSprite;
        [SerializeField] private Button replaceButton;
        [SerializeField] private IAPButton removeAdsButton;
        [SerializeField] private IAPButton restoreButton;

        private Dictionary<string, LocationPayload> locationDict = new Dictionary<string, LocationPayload>();
        public static readonly string IS_REPLACING_PP_KEY = "REPLACE_LOCATION";


        // Start is called before the first frame update
        protected override void Start()
        {
            AdsManager.Instance.SetVideoAdButton(replaceButton, ReplaceLocation, false);
            locationDropdown.options.Insert(0, new Dropdown.OptionData("Loading..."));

            base.Start();
        }

        protected override void Update()
        {
            base.Update();

            restoreButton.gameObject.SetActive(!AdsManager.Instance.IsRemoveAds);
            removeAdsButton.gameObject.SetActive(!AdsManager.Instance.IsRemoveAds);
            replaceButton.interactable = AdsManager.Instance.IsReady && locationDropdown.value > 0;

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
            locationDict = await FetchUserLocationAnchorIdsAsync(UserId);

            foreach (LocationPayload loc in locationDict.Values)
            {
                locationDropdown.options.Add(new Dropdown.OptionData(loc.ToString(), dropdownItemSprite));
            }

            locationDropdown.options[0].text = "";
            locationDropdown.Hide();

            SplashText("Warning - This Will Delete The Selected Location", Color.yellow);
        }

        public void OnBackButtonPressed()
        {
            if (SceneManager.GetActiveScene().name != "AzureSpatialAnchor")
                SceneManager.LoadSceneAsync("AzureSpatialAnchor");
        }

        public void OnDropdownValueChanged()
        {
            if (!AdsManager.Instance.IsRemoveAds)
                SplashText("Must Watch Ads To Replace This Location", Color.white);
        }

        public async void ReplaceLocation()
        {
            string locationItem = locationDropdown.options[locationDropdown.value].text;

            if (!string.IsNullOrEmpty(locationItem))
            {
                foreach (var entry in locationDict)
                {
                    LocationPayload payload = entry.Value;

                    if (locationItem == payload.ToString())
                    {
                        try
                        {
                            await DeleteLocationAsync(entry.Key);

                            UnityDispatcher.InvokeOnAppThread(() =>
                            {
                                PlayerPrefs.SetString(ANCHOR_ID_TO_DELETE_PP_KEY, entry.Key);
                                PlayerPrefs.SetInt(IS_REPLACING_PP_KEY, 1);
                                PlayerPrefs.SetInt(IS_CREATE_MODE_PP_KEY, 1);
                                PlayerPrefs.Save();

                                SceneManager.LoadSceneAsync("AzureSpatialAnchor");
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
            DatabaseReference locationReference = FirebaseDatabase.DefaultInstance.GetReference("users/" + UserId + "/locations/" + locationId);

            try
            {
                DataSnapshot snapshot = await locationReference.GetValueAsync();

                if (snapshot.Exists && snapshot.HasChild("filePath"))
                {
                    string filePath = snapshot.Child("filePath").Value.ToString();
                    string fileName = snapshot.Child("fileName").Value.ToString();
                    string fileDesc = snapshot.Child("fileDesc").Value.ToString();

                    TranslateModel = new ModelPayload(fileName, filePath, fileDesc);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            await locationReference.RemoveValueAsync();
        }
    }
}