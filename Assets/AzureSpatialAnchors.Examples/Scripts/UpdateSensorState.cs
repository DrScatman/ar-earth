// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
using UnityEngine;
using UnityEngine.UI;

using Microsoft.Azure.SpatialAnchors.Unity.Examples;

public class UpdateSensorState : MonoBehaviour
{
    public enum Sensor
    {
        GeoLocation,
        Wifi,
        Bluetooth
    }

    //Commenting Out for Testing
    //public AzureSpatialAnchorsCoarseRelocDemoScript CoarseRelocDemoScript;
    //Added in for Testing
    public AzureSpatialAnchorController BasicDemoScript;
    public Sensor SensorType;
    public Image Icon;

    public Color AvailableColor = Color.green;
    public Color DisabledCapabilityColor = Color.red;
    public Color MissingProviderColor = Color.yellow;
    public Color NoDataColor = Color.black;

    // Start is called before the first frame update
    void Start()
    {
        if (Icon == null)
        {
            return;
        }
        Icon.color = MissingProviderColor;
    }

    // Update is called once per frame
    void Update()
    {
        if (Icon == null)
        {
            return;
        }
        //if (CoarseRelocDemoScript == null)
        if (BasicDemoScript == null || SensorProviderHelper.locationProvider == null)
        {
            Icon.color = MissingProviderColor;
            return;
        }

        switch (SensorType)
        {
            case Sensor.GeoLocation:
                //UpdateColor(CoarseRelocDemoScript.GeoLocationStatus);
                UpdateColor(SensorProviderHelper.GeoLocationStatus);
                break;
            case Sensor.Wifi:
                //UpdateColor(CoarseRelocDemoScript.WifiStatus);
                UpdateColor(SensorProviderHelper.WifiStatus);
                break;
            case Sensor.Bluetooth:
                //UpdateColor(CoarseRelocDemoScript.BluetoothStatus);
                UpdateColor(SensorProviderHelper.BluetoothStatus);
                break;
        }
    }

    private void UpdateColor(SensorStatus status)
    {
        switch (status)
        {
            case SensorStatus.Available:
                Icon.color = AvailableColor;
                break;
            case SensorStatus.DisabledCapability:
                Icon.color = DisabledCapabilityColor;
                break;
            case SensorStatus.MissingSensorFingerprintProvider:
                Icon.color = MissingProviderColor;
                break;
            case SensorStatus.NoData:
                Icon.color = NoDataColor;
                break;
        }
    }
}