using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class IapManager : IAPListener
    {
        [SerializeField] private AzureSpatialAnchorController azureController;
        [SerializeField] private GameObject locationOverlay;
        [SerializeField] private bool hasTokens;

        public static IapManager Instance { get; private set; }

        private readonly List<Product> completedPurchases = new List<Product>();
        private bool hasFreeTokens;
        public static readonly string REM_ADS_IAP_ID = "remove_ads";
        private static readonly string NUM_FREE_USED_PP_KEY = "sdbh";
        private int numFree = 1;
        private int numFreeUsed = 0;


        void Awake()
        {
            if (Instance == null || FindObjectOfType<IapManager>() == null)
            {
                Instance = this;

                if (base.dontDestroyOnLoad)
                    DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
#if UNITY_EDITOR
            PlayerPrefs.DeleteKey(ReplaceLocationUIController.IS_REPLACING_PP_KEY);
#endif
            ToggleLocationOverlay(false);
            hasFreeTokens = IsReplacingLocation(true);
            numFreeUsed = PlayerPrefs.GetInt(NUM_FREE_USED_PP_KEY, 0);
        }

        void Update()
        {
            if (SceneManager.GetActiveScene().name == "AzureSpatialAnchor" && SceneManager.GetActiveScene().isLoaded)
            {
                if (azureController != null)
                {
                    hasFreeTokens = (azureController.firebaseLoader.IsReady && azureController.firebaseLoader.NumMyLocations < numFree && !HasUsedFreeLocations())
                                        || IsReplacingLocation(false);
                    hasTokens = GetNumTokens() >= 1 || hasFreeTokens;
                }
                else
                {
                    azureController = FindObjectOfType<AzureSpatialAnchorController>();

                    if (azureController != null)
                    {
                        Button createButton = azureController.createLocationButton.GetComponent<Button>();
                        createButton.onClick.RemoveAllListeners();
                        createButton.onClick.AddListener(OnCreateLocationPressed);
                    }
                }
            }
        }

        public bool HasUsedFreeLocations()
        {
            return numFreeUsed >= numFree;
        }

        public void OnCreateLocationPressed()
        {
            if (hasTokens)
            {
                ToggleLocationOverlay(false);
                UnityDispatcher.InvokeOnAppThread(() => azureController.ToggleCreateMode(true));
            }
            else
            {
                ToggleLocationOverlay(true);
            }
        }

        public void OnProductPurchaseRecognized(Product product)
        {
            PayoutDefinition payout = product.definition.payout;
            Debug.Log(string.Format("Granting {0} {1} {2} {3}", payout.quantity, payout.typeString, payout.subtype, payout.data));

            if (product.definition.storeSpecificId == REM_ADS_IAP_ID || product.definition.id == REM_ADS_IAP_ID)
            {
                AdsManager.Instance.IsRemoveAds = true;
                AdsManager.Instance.ResetButtonListeners();

                PlayerPrefs.SetInt(AdsManager.REMOVE_ADS_PP_KEY, 1);
                PlayerPrefs.Save();

                base.consumePurchase = true;
            }
            else
            {
                if (!completedPurchases.Contains(product))
                    completedPurchases.Add(product);

                ToggleLocationOverlay(false);

                base.consumePurchase = false;
            }
        }

        public void OnProductPurchaseFailure(Product product, PurchaseFailureReason reason)
        {
            Debug.LogError("Failed to purchase product: " + product);
            Debug.LogError(reason);
            FindObjectOfType<FirebaseManager>().SplashText(reason.ToString(), Color.red);
        }

        public void DisableLocationOverlay() { ToggleLocationOverlay(false); }

        public void ToggleLocationOverlay(bool active)
        {
            if (locationOverlay == null)
            {
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.tag == "UI Overlay")
                    {
                        locationOverlay = go;
                    }
                }

                locationOverlay.GetComponentInChildren<Button>(true).onClick.AddListener(DisableLocationOverlay);
            }

            locationOverlay.SetActive(active);

            if (active)
                Firebase.Analytics.FirebaseAnalytics.LogEvent("new_location_overlay", "view", 1);
        }

        private double GetNumTokens()
        {
            double tokens = 0;

            foreach (Product product in completedPurchases)
            {
                tokens += product.definition.payout.quantity;
            }
            return tokens;
        }

        public void ConsumeTokens(int numToConsume)
        {
            FirebaseManager manager = FindObjectOfType<FirebaseManager>();
            // hasFreeTokens = IsReplacingLocation(true) || (manager != null && manager.IsReady && manager.NumMyLocations < numFree && !HasUsedFreeLocations());

            if (completedPurchases.Count > 0)
            {
                for (int i = 0; i < numToConsume; i++)
                {
                    if (i < completedPurchases.Count)
                    {
                        Debug.Log("Purchased Token Consumed");
                        ConfirmPendingPurchase(completedPurchases[i]);
                        completedPurchases.Remove(completedPurchases[i]);
                    }
                    else
                    {
                        Debug.LogError("Index Out Of Bounds:\tNot enough completed purchases to consume " + numToConsume + " tokens");
                    }
                }
            }
            else
            {
                Debug.Log("Free Token Consumed");
                FirebaseManager.TranslateModel = null;
                hasReplaceKey = false;

                if (PlayerPrefs.HasKey(ReplaceLocationUIController.IS_REPLACING_PP_KEY))
                    PlayerPrefs.DeleteKey(ReplaceLocationUIController.IS_REPLACING_PP_KEY);

                if (numFreeUsed < numFree)
                    PlayerPrefs.SetInt(NUM_FREE_USED_PP_KEY, (numFreeUsed += numToConsume));

                PlayerPrefs.Save();
            }
        }

        private void ConfirmPendingPurchase(Product product)
        {
            CodelessIAPStoreListener.Instance.StoreController.ConfirmPendingPurchase(product);
        }

        private bool hasReplaceKey;

        public bool IsReplacingLocation(bool testPP)
        {
            if (testPP)
                hasReplaceKey = PlayerPrefs.HasKey(ReplaceLocationUIController.IS_REPLACING_PP_KEY);

            return FirebaseManager.TranslateModel != null || hasReplaceKey;
        }
    }
}