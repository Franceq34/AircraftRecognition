using UnityEngine;


public class PlaneVelocity : MonoBehaviour {
    
    void Start ()
    {

    }
    
    void Update () {
        transform.position += transform.forward * Time.deltaTime * Settings.Instance.speed;
    }
}
