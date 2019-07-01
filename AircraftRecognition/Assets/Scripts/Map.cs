using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Map : MonoBehaviour
{

    public string label;
    public string name;
    public int altitude;

    public Map(string plabel, string pname, int paltitude)
    {
        label = plabel;
        name = pname;
        altitude = paltitude;
    }
}
