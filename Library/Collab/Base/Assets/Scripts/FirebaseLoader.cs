using Firebase.Database;
using Firebase.Storage;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.UI;

namespace Microsoft.Azure.SpatialAnchors.Unity.Examples
{
    public class FirebaseLoader : FirebaseManager
    {
        [Header("Location Data UI")]
        [SerializeField] private InputField locationNameInput;
        [SerializeField] private InputField locationDescInput;
        [SerializeField] private Button locationSaveButton;

        private string anchorIdToWrite;
        public string AnchorIdToWrite { get => anchorIdToWrite; set => anchorIdToWrite = value; }

        public void ToggleLocationDataUI(bool active)
        {
            locationSaveButton.transform.parent.gameObject.SetActive(active);

            XRUXPicker.Instance.GetFeedbackText().gameObject.SetActive(!active);
            foreach (Button b in XRUXPicker.Instance.GetDemoButtons())
            {
                b.gameObject.SetActive(!active);
            }
        }

        public void OnWriteButtonPressed()
        {
            WriteAnchorIDToFirebaseAsync(AnchorIdToWrite, locationNameInput.text, locationDescInput.text);
            ToggleLocationDataUI(false);
        }

        public void WriteAnchorIDToFirebaseAsync(string anchorId, string name, string description)
        {
            Status = "Writting anchorId";

            Dictionary<string, object> childUpdates = new Dictionary<string, object>();
            childUpdates[anchorId] = new LocationData(name, description).ToDictionary();

            FirebaseDatabase.DefaultInstance.GetReference(FIREBASE_LOCATIONS_TABLE_KEY)
                .UpdateChildrenAsync(childUpdates)
                .ContinueWith(task =>
                {
                    if (task.IsCanceled || task.IsFaulted)
                    {
                        Debug.LogError(task.Exception.Message, this);
                        Status = task.Exception.Message;
                        return;
                    };

                    Debug.Log("AnchorId written");
                    Status = "AnchorId written";
                });
        }

        protected override void PreLoadAllModelsForUser()
        {
            foreach (string locationId in querriedLocationAnchorIds)
            {
                Status = "Preload " + selectedUserId + " - " + locationId;
                LoadOrMoveUserModel(transform, locationId, false);
            }
        }

        public void LoadOrMoveUserModel(Transform anchoredTransform, string anchorId, bool setActive = true)
        {
            if (spawnedObjects.ContainsKey(anchorId))
            {
                Status = "Moving model";
                spawnedObjects[anchorId].transform.position = anchoredTransform.position;
                spawnedObjects[anchorId].SetActive(setActive);

                TryDisableMesh(anchoredTransform.gameObject);
            }
            else
            {
                if (!hasAnchorLocations)
                {
                    Debug.LogError("Attempted to load model with no qurried locations for user");
                    return;
                }

                Status = "Querrying database for model";
                LoadModelFromFirebase(anchoredTransform, anchorId, setActive);
            }
        }

        private void LoadModelFromFirebase(Transform anchoredTransform, string locationId, bool setActive = true)
        {
            string fileName = "";
            string position = "0.0,0.0,0.0";
            string rotation = "0.0,0.0,0.0";
            string scale = "1.0,1.0,1.0";

            var taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            FirebaseDatabase.DefaultInstance.GetReference(FIREBASE_USERS_TABLE_KEY + "/" + selectedUserId + "/locations/" + locationId).GetValueAsync().ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError(task.Exception.Message, this);
                    Status = task.Exception.Message;
                    return;
                }

                if (task.IsCompleted)
                {
                    foreach (DataSnapshot prop in task.Result.Children)
                    {
                        switch (prop.Key)
                        {
                            case "fileName":
                                fileName = prop.Value.ToString();
                                break;
                            case "position":
                                position = prop.Value.ToString();
                                break;
                            case "rotation":
                                rotation = prop.Value.ToString();
                                break;
                            case "scale":
                                scale = prop.Value.ToString();
                                break;
                            default:
                                Debug.LogWarning("Unrecognized property  |  Key: " + prop.Key + "  |  Value: " + prop.Value, this);
                                //Status = "Unrecognized property  |  Key: " + prop.Key + "  |  Value: " + prop.Value;
                                break;
                        }
                    }

                    // Retreive file from Firebase Storage via fileName & generate a download URL
                    FirebaseStorage storage = FirebaseStorage.DefaultInstance;
                    StorageReference reference = storage.GetReference(selectedUserId + "/" + fileName);

                    reference.GetDownloadUrlAsync().ContinueWith(task2 =>
                    {
                        if (task2.IsFaulted || task2.IsCanceled)
                        {
                            Debug.LogError(task2.Exception.Message + "  |  Path: " + reference.Path + "\n" + task2.Exception.StackTrace, this);
                            Status = task2.Exception.Message;
                            return;
                        }

                        if (task2.IsCompleted)
                        {
                            string uri = task2.Result.AbsoluteUri;

                            Status = "GLTFast - Loading model...";
                            LoadGltf(uri, anchoredTransform, position, rotation, scale, locationId, setActive);
                        }

                    }, taskScheduler);
                }
            });
        }

        private void LoadGltf(string modelUrl, Transform anchoredTransform, string firebasePosition, string firebaseRotation, string firebaseScale, string locationId, bool setActive)
        {
            var gltf = new GameObject().AddComponent<GLTFast.GltfAsset>();
            gltf.url = modelUrl;

            gltf.transform.position = (StringToVector(firebasePosition, ',') + anchoredTransform.position);
            Vector3 rotVector = StringToVector(firebaseRotation, ',');
            gltf.transform.rotation = Quaternion.Euler(rotVector.x, rotVector.y, rotVector.z);
            gltf.transform.localScale = StringToVector(firebaseScale, ',');

            gltf.onLoadComplete += (g, isLoaded) =>
            {
                TryDisableMesh(anchoredTransform.gameObject);

                GameObject obj = g.gameObject;
                obj.SetActive(setActive);
                spawnedObjects.Add(locationId, obj);
                Status = "Model loaded - " + locationId;

                OnGltfLoadComplete(obj, isLoaded);
            };
        }

        public virtual void OnGltfLoadComplete(GameObject obj, bool isLoaded)
        {
            // To be overriden
        }

        private Vector3 StringToVector(string str, char del)
        {
            Vector3 vector;
            string[] vals = str.Split(del);

            vector.x = float.Parse(vals[0]);
            vector.y = float.Parse(vals[1]);
            vector.z = float.Parse(vals[2]);
            return vector;
        }

        private bool TryDisableMesh(GameObject obj)
        {
            if (obj.TryGetComponent<MeshRenderer>(out MeshRenderer mesh) && mesh != null)
            {
                mesh.enabled = false;
                return true;
            }

            return false;
        }

        private class LocationData
        {
            private string name;
            private string description;

            public LocationData(string name, string description)
            {
                this.name = name;
                this.description = description;
            }

            public Dictionary<string, object> ToDictionary()
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["name"] = name;
                result["description"] = description;

                return result;
            }
        }
    }
}
