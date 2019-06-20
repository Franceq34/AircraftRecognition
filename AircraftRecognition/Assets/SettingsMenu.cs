using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMenu : MonoBehaviour {

	public void setAltitude(float altitude)
    {
        Settings.Instance.altitude = altitude;
    }

    public void setSpeed(float speed)
    {
        Settings.Instance.speed = speed;
    }

    public void setCloseness(float closeness)
    {
        Settings.Instance.closeness = closeness;
    }
}
