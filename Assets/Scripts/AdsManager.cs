using UnityEngine.Events;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class AdsManager : MonoBehaviour, IUnityAdsListener
    {
        public static AdsManager Instance { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsRemoveAds { get; set; }

        private Button button;
        private UnityAction onAdsCompleted;
        private string myPlacementId;
        public static readonly string REMOVE_ADS_PP_KEY = "QAZZAQ";


        void Awake()
        {
            if (Instance == null || FindObjectOfType<IapManager>() == null)
            {
                Instance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            IsRemoveAds = PlayerPrefs.GetInt(REMOVE_ADS_PP_KEY, 0) == 1;
            Init();
        }

        private void Init()
        {
            Advertisement.AddListener(this);
#if UNITY_IOS
            Advertisement.Initialize("3596418");
#elif UNITY_ANDROID
            Advertisement.Initialize("3596419");
#else
            Advertisement.Initialize("3596419", true);
#endif
        }

        public void SetVideoAdButton(Button button, UnityAction onCompleted, bool isSkippable)
        {
            this.button = button;
            onAdsCompleted = onCompleted;
            myPlacementId = isSkippable ? "rewardedSkippableVideo" : "rewardedVideo";

            ResetButtonListeners();
        }

        public void ResetButtonListeners()
        {
            if (button != null)
            {
                if (!IsRemoveAds)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(ShowAds);

                    if (!Advertisement.isInitialized)
                        Init();

                    IsReady = Advertisement.IsReady(this.myPlacementId)
                        || !Advertisement.isSupported
                        || (Advertisement.GetPlacementState() == PlacementState.Disabled)
                        || (Advertisement.GetPlacementState() == PlacementState.NotAvailable)
                        || (Advertisement.GetPlacementState() == PlacementState.NoFill)
                        || (Advertisement.GetPlacementState() == PlacementState.Ready);
                }
                else
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(onAdsCompleted);
                    IsReady = true;
                }
            }
        }

        private void ShowAds()
        {
            Advertisement.Show(myPlacementId);
        }

        public void OnUnityAdsReady(string placementId)
        {
            if (placementId == myPlacementId)
            {
                IsReady = true;
            }
        }

        public void OnUnityAdsDidStart(string placementId) { }

        public void OnUnityAdsDidFinish(string placementId, ShowResult showResult)
        {
            if (showResult == ShowResult.Finished || showResult == ShowResult.Skipped)
            {
                if (onAdsCompleted != null)
                    onAdsCompleted();
                else if (SceneManager.GetActiveScene().name == "ReplaceLocation UI")
                    FindObjectOfType<ReplaceLocationUIController>().ReplaceLocation();
                else if (SceneManager.GetActiveScene().name == "Menu UI")
                    FindObjectOfType<MenuUIController>().StartAzureSpatialAnchor();
                else
                    Debug.LogError("Nothing To Invoke On Ad Completion");
            }
            else if (showResult == ShowResult.Failed)
            {
                Debug.LogWarning("The ad did not finish due to an error.");
            }
        }

        public void OnUnityAdsDidError(string message)
        {
            Debug.LogError("Unity Ads Error:\t" + message);
        }

    }
}
