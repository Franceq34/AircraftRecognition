using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MapDropdown : MonoBehaviour
{
    List<Map> names = Settings.Instance.maps;

    public TMPro.TMP_Dropdown dropdown;

    public void DropdownIndexChanged(int index)
    {
        Settings.Instance.setMap(Settings.Instance.maps[index]);
    }

    private void Start()
    {
        PopulateList();
    }

    void PopulateList()
    {
        List<string> labels = Settings.Instance.maps.Select(o => o.label).ToList();
        dropdown.AddOptions(labels);
    }
}
