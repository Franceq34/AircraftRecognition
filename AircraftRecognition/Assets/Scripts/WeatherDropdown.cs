using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeatherDropdown : MonoBehaviour
{
    List<string> names = new List<string>() { "Sunny", "Rainy", "Cloudy" };

    public TMPro.TMP_Dropdown dropdown;

    public void DropdownIndexChanged(int index)
    {
        Settings.Instance.setWeather(names[index]);
    }

    private void Start()
    {
        PopulateList();
    }

    void PopulateList()
    {
        dropdown.AddOptions(names);
    }
}
