using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class PlanesSpawn : MonoBehaviour {
    
    public Transform player;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Use this for initialization
    void Start ()
    {
        ReplacePlane();
    }

    // Update is called once per frame
    void Update () {
        transform.position += transform.forward * Time.deltaTime * Settings.Instance.speed;
        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            ReplacePlane();
        }
        if (Input.GetKeyDown(KeyCode.Return))
        {
            SceneManager.LoadScene("main");
        }

    }

    void ReplacePlane()
    {
        //SET POSITION
        Vector3 pos = UnityEngine.Random.insideUnitCircle * 6000;
        Vector3 poscentered = new Vector3(pos.x + 350, Settings.Instance.altitude, pos.y + 300);
        rb.transform.position = poscentered;
        //SET ROTATION
        Vector3 direction = player.position - transform.position;
        direction = AlterDirection(direction);
        Quaternion rotation = Quaternion.LookRotation(direction);
        rotation.x = 0;
        rotation.z = 0;
        rb.transform.rotation = rotation;
    }

    private Vector3 AlterDirection(Vector3 direction)
    {
        var max = Settings.Instance.closeness;
        direction.x = direction.x + UnityEngine.Random.Range(-max, max);
        direction.z = direction.z + UnityEngine.Random.Range(-max, max);
        return direction;
    }
}
