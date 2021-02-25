using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Linq;
using UnityEngine.SceneManagement;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class IapManager : IAPListener
    {
        [SerializeField] private MenuUIController menuUIController;
        [SerializeField] private IAPButton iapButton;
        [SerializeField] private GameObject locationOverlay;
        [SerializeField] private bool hasTokens;

        private static readonly List<Product> completedPurchases = new List<Product>();
        private bool hasFreeTokens;


        void Awake()
        {
            if (base.dontDestroyOnLoad)
                DontDestroyOnLoad(this.gameObject);
        }

        void Start()
        {
            // Necessary if logging out and back in since dont destroy on load
            if (SceneManager.GetActiveScene().name == "Menu UI")
            {
                if (menuUIController == null)
                    menuUIController = FindObjectOfType<MenuUIController>();
                if (iapButton == null)
                    iapButton = Resources.FindObjectsOfTypeAll<IAPButton>()[0];
                if (locationOverlay == null)
                {
                    foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (go.tag == "UI Overlay")
                        {
                            locationOverlay = go;
                        }
                    }
                }
            }

            ToggleLocationOverlay(false);
            hasFreeTokens = IsReplacingLocation();
        }

        void Update()
        {
            if (menuUIController != null)
            {
                if (menuUIController.HasAnchorLocations && !IsReplacingLocation())
                {
                    hasFreeTokens = false;
                }
                else
                {
                    hasFreeTokens = true;
                }
            }

            hasTokens = GetNumTokens() >= 1 || hasFreeTokens;
        }

        public void OnCreateLocationPressed()
        {
            if (hasTokens)
            {
                ToggleLocationOverlay(false);
                UnityDispatcher.InvokeOnAppThread(() => menuUIController.LoadCreateLocationScene());
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
            completedPurchases.Add(product);

            ToggleLocationOverlay(GetNumTokens() < 1 && !hasFreeTokens);
        }

        public void OnProductPurchaseFailure(Product product, PurchaseFailureReason reason)
        {
            Debug.LogError("Failed to purchase product: " + product);
            Debug.LogError(reason);
            menuUIController.SplashText(reason.ToString(), Color.red);
        }

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
            }

            locationOverlay.SetActive(active);
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
            if (completedPurchases.Count > 0)
            {
                for (int i = 0; i < numToConsume; i++)
                {
                    if (i < completedPurchases.Count)
                    {
                        Debug.Log("Purchased Token Consumed");
                        CodelessIAPStoreListener.Instance.StoreController.ConfirmPendingPurchase(completedPurchases[i]);
                        completedPurchases.Remove(completedPurchases[i]);
                    }
                    else
                    {
                        Debug.LogError("Index Out Of Bounds:\tNot enough completed purchases to consume " + numToConsume + " tokens");
                    }
                }
            }
            else if (hasFreeTokens)
            {
                Debug.Log("Free Token Consumed");
                hasFreeTokens = false;

                if (IsReplacingLocation())
                {
                    FirebaseManager.TranslateModel = null;

                    if (PlayerPrefs.HasKey(ReplaceLocationUIController.IS_REPLACING_PP_KEY))
                        PlayerPrefs.DeleteKey(ReplaceLocationUIController.IS_REPLACING_PP_KEY);
                }

                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogError("FAILED TO CONSUME TOKEN");
            }
        }

        public bool IsReplacingLocation()
        {
            return FirebaseManager.TranslateModel != null || PlayerPrefs.HasKey(ReplaceLocationUIController.IS_REPLACING_PP_KEY);
        }
    }
}