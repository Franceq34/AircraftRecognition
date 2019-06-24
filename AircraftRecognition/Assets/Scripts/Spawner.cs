using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour {

    public GameObject[] planes;
    public bool stop;

    int randPlane;
    
	void Start ()
    {
        StartCoroutine(waitSpawner());
	}
	
	void Update ()
    {
		
	}

    IEnumerator waitSpawner()
    {
        while (!stop)
        {
            randPlane = Random.Range(0, planes.Length);
            //SET POSITION
            Vector3 pos = UnityEngine.Random.insideUnitCircle * 6000;
            Vector3 poscentered = new Vector3(pos.x + 350, Settings.Instance.altitude, pos.y + 300);

            //SET ROTATION
            Vector3 direction = transform.position - poscentered;
            direction = AlterDirection(direction);
            Quaternion rotation = Quaternion.LookRotation(direction);
            rotation.x = 0;
            rotation.z = 0;

            Instantiate(planes[randPlane], poscentered, rotation);

            yield return new WaitForSeconds(10);
        }
    }

    private Vector3 AlterDirection(Vector3 direction)
    {
        var max = Settings.Instance.closeness;
        direction.x = direction.x + UnityEngine.Random.Range(-max, max);
        direction.z = direction.z + UnityEngine.Random.Range(-max, max);
        return direction;
    }
}
