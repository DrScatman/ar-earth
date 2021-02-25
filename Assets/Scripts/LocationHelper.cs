using System.Collections;
using UnityEngine.Networking;
using UnityEngine;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public static class LocationHelper
    {
        public static readonly string COUNTRY_CODE_PP_KEY = "country_code";
        public static bool isDetected;

        public static IEnumerator DetectCountry()
        {
            if (!PlayerPrefs.HasKey(COUNTRY_CODE_PP_KEY) || PlayerPrefs.GetString(COUNTRY_CODE_PP_KEY, "") == "")
            {
                UnityWebRequest request = UnityWebRequest.Get("https://extreme-ip-lookup.com/json");
                yield return request.SendWebRequest();

                if (request.isHttpError || request.isNetworkError)
                {
                    Debug.LogError(request.error);
                    GameObject.FindObjectOfType<FirebaseManager>().SplashText("Network Error", Color.red);
                }
                else
                {
                    if (request.isDone)
                    {
                        Country res = JsonUtility.FromJson<Country>(request.downloadHandler.text);
                        PlayerPrefs.SetString(COUNTRY_CODE_PP_KEY, res.countryCode);
                        isDetected = true;
                        Debug.Log(res.countryCode);
                    }
                }
            }
            else
            {
                isDetected = true;
            }
        }

        /// <summary>
        /// Helps to convert Unity's Application.systemLanguage to a 
        /// 2 letter ISO country code. There is unfortunately not more
        /// countries available as Unity's enum does not enclose all
        /// countries.
        /// </summary>
        /// <returns>The 2-letter ISO code from system language.</returns>
        public static string Get2LetterISOCodeFromSystemLanguage()
        {
            SystemLanguage lang = Application.systemLanguage;
            string res = "EN";
            switch (lang)
            {
                case SystemLanguage.Afrikaans: res = "AF"; break;
                case SystemLanguage.Arabic: res = "AR"; break;
                case SystemLanguage.Basque: res = "EU"; break;
                case SystemLanguage.Belarusian: res = "BY"; break;
                case SystemLanguage.Bulgarian: res = "BG"; break;
                case SystemLanguage.Catalan: res = "CA"; break;
                case SystemLanguage.Chinese: res = "ZH"; break;
                case SystemLanguage.Czech: res = "CS"; break;
                case SystemLanguage.Danish: res = "DA"; break;
                case SystemLanguage.Dutch: res = "NL"; break;
                case SystemLanguage.English: res = "EN"; break;
                case SystemLanguage.Estonian: res = "ET"; break;
                case SystemLanguage.Faroese: res = "FO"; break;
                case SystemLanguage.Finnish: res = "FI"; break;
                case SystemLanguage.French: res = "FR"; break;
                case SystemLanguage.German: res = "DE"; break;
                case SystemLanguage.Greek: res = "EL"; break;
                case SystemLanguage.Hebrew: res = "IW"; break;
                case SystemLanguage.Hungarian: res = "HU"; break;
                case SystemLanguage.Icelandic: res = "IS"; break;
                case SystemLanguage.Indonesian: res = "IN"; break;
                case SystemLanguage.Italian: res = "IT"; break;
                case SystemLanguage.Japanese: res = "JA"; break;
                case SystemLanguage.Korean: res = "KO"; break;
                case SystemLanguage.Latvian: res = "LV"; break;
                case SystemLanguage.Lithuanian: res = "LT"; break;
                case SystemLanguage.Norwegian: res = "NO"; break;
                case SystemLanguage.Polish: res = "PL"; break;
                case SystemLanguage.Portuguese: res = "PT"; break;
                case SystemLanguage.Romanian: res = "RO"; break;
                case SystemLanguage.Russian: res = "RU"; break;
                case SystemLanguage.SerboCroatian: res = "SH"; break;
                case SystemLanguage.Slovak: res = "SK"; break;
                case SystemLanguage.Slovenian: res = "SL"; break;
                case SystemLanguage.Spanish: res = "ES"; break;
                case SystemLanguage.Swedish: res = "SV"; break;
                case SystemLanguage.Thai: res = "TH"; break;
                case SystemLanguage.Turkish: res = "TR"; break;
                case SystemLanguage.Ukrainian: res = "UK"; break;
                case SystemLanguage.Unknown: res = "EN"; break;
                case SystemLanguage.Vietnamese: res = "VI"; break;
            }
            //		Debug.Log ("Lang: " + res);
            return res;
        }

        public class Country
        {
            public string businessName;
            public string businessWebsite;
            public string city;
            public string continent;
            public string country;
            public string countryCode;
            public string ipName;
            public string ipType;
            public string isp;
            public string lat;
            public string lon;
            public string org;
            public string query;
            public string region;
            public string status;

        }
    }
}