using UnityEngine;

[System.Serializable]
public class ModelSpawnData
{
    public GameObject obj;
    public Vector3 positionOffset;
    public Quaternion rotationOffset;
    public Vector3 scale;

    public ModelSpawnData(GameObject obj, Vector3 positionOffset, Quaternion rotationOffset, Vector3 scale)
    {
        this.obj = obj;
        this.positionOffset = positionOffset;
        this.rotationOffset = rotationOffset;
        this.scale = scale;
    }
}
