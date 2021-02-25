using UnityEngine.Events;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.UI;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class AdsManager : MonoBehaviour, IUnityAdsListener
    {
        public UnityEvent onAdsCompleted;

        private Button button;
        private string myPlacementId;
        

        void Start()
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

        public void SetRewardedVideoButton(Button button, bool isSkippable)
        {
            this.button = button;
            this.myPlacementId = isSkippable ? "rewardedSkippableVideo" : "rewardedVideo";
            
            this.button.interactable = Advertisement.IsReady(myPlacementId);
            this.button.onClick.AddListener(OnButtonClick);
        }

        private void OnButtonClick()
        {
            Advertisement.Show(myPlacementId);
        }

        public void OnUnityAdsReady(string placementId)
        {
            if (placementId == myPlacementId)
            {
                button.interactable = true;
            }
        }

        public void OnUnityAdsDidStart(string placementId) { }

        public void OnUnityAdsDidFinish(string placementId, ShowResult showResult)
        {
            if (showResult == ShowResult.Finished)
            {
                onAdsCompleted.Invoke();
            }
            else if (showResult == ShowResult.Skipped)
            {
                onAdsCompleted.Invoke();
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
