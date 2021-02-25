using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using System.Linq;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class IapManager : IAPListener
    {
        [SerializeField] private MenuUIController menuUIController;
        [SerializeField] private IAPButton iapButton;
        [SerializeField] private GameObject locationOverlay;
        [SerializeField] private bool hasTokens;

        private readonly List<Product> completedPurchases = new List<Product>();
        //public static readonly string TOKEN_CONSUMED_DEVICE_LOCK_KEY = "xb115tn!";
        private bool hasFreeTokens;
        private bool isReplacing;


        void Start()
        {
            // Necessary if logging out and back in since dont destroy on load
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

            ToggleLocationOverlay(false);
            isReplacing = PlayerPrefs.HasKey(FirebaseManager.TRANSLATE_FILEPATH_PP_KEY);
            hasFreeTokens = isReplacing;
            //hasFreeTokens = !PlayerPrefs.HasKey(TOKEN_CONSUMED_DEVICE_LOCK_KEY) || isReplacing;
        }

        void Update()
        {
            if (menuUIController != null)
            {
                if (hasFreeTokens && menuUIController.HasAnchorLocations && !isReplacing)
                {
                    hasFreeTokens = false;
                }
            }

            hasTokens = GetNumTokens() >= 1 || hasFreeTokens;
        }

        public void OnCreateLocationPressed()
        {
            if (hasTokens)
            {
                locationOverlay.SetActive(false);
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
                for (int i = 0;  i < numToConsume; i++)
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
                isReplacing = false;

                //PlayerPrefs.SetString(TOKEN_CONSUMED_DEVICE_LOCK_KEY, TOKEN_CONSUMED_DEVICE_LOCK_KEY);
                if (PlayerPrefs.HasKey(FirebaseManager.TRANSLATE_FILEPATH_PP_KEY))
                {
                    PlayerPrefs.DeleteKey(FirebaseManager.TRANSLATE_FILEPATH_PP_KEY);
                }
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogError("FAILED TO CONSUME TOKEN");
            }
        }
    }
}