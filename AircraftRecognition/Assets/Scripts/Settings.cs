using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Settings : MonoBehaviour
{


    //ATTRIBUTES

    public static Settings Instance { get; private set; }

    public float altitude = 200;

    public float speed = 600;

    public float closeness = 0;

    public Map mapSelected;

    public string weatherSelected = "sunny";

    public List<Map> maps = new List<Map>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            maps.Add(new Map("Mountains", "mountains", 0));
            maps.Add(new Map("Island", "island", 150));
            mapSelected = maps[0];
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


    //SETTERS

    public void setAltitude(float altitude)
    {
        Instance.altitude = altitude;
    }

    public void setSpeed(float speed)
    {
        Instance.speed = speed;
    }

    public void setCloseness(float closeness)
    {
        Instance.closeness = closeness;
    }

    public void setMap(Map map)
    {
        Instance.mapSelected = map;
    }

    public void setWeather(string weather)
    {
        Instance.weatherSelected = weather;
    }
}
