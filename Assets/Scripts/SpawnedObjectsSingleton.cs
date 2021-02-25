using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SpawnedObjectsSingleton : MonoBehaviour
{
    public static SpawnedObjectsSingleton Instance { get; private set; }
    public Dictionary<string, GameObject> SpawnedObjects { get => spawnedObjects != null ? spawnedObjects : new Dictionary<string, GameObject>(); }
    private Dictionary<string, GameObject> spawnedObjects;


    private void Awake()
    {
        if (Instance == null || FindObjectOfType<SpawnedObjectsSingleton>() == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
            RefreshSpawnedObjects();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void RefreshSpawnedObjects()
    {
        spawnedObjects = new Dictionary<string, GameObject>();

        foreach (Transform model in Instance.transform)
        {
            if (SpawnedObjects.ContainsKey(model.name))
            {
                SpawnedObjects[model.name] = model.gameObject;
            }
        }
    }

    public bool RemoveSpawnedObject(string locationId)
    {
        try
        {
            if (Instance != null)
            {
                if (SpawnedObjects.ContainsKey(locationId))
                {
                    GameObject obj = SpawnedObjects[locationId];

                    if (obj != null)
                        Destroy(obj);

                    SpawnedObjects.Remove(locationId);
                    return true;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            SpawnedObjects.Clear();
        }

        return false;
    }
}
