using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlanesSpawn : MonoBehaviour {

    private Vector3 spawnPos;
    private Quaternion spawnRot;
    public Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnPos = rb.position;
        spawnRot = rb.rotation;
    }

    // Use this for initialization
    void Start ()
    {
        StartCoroutine(ReplacePlane());
    }

    // Update is called once per frame
    void Update () {
		
	}

    IEnumerator ReplacePlane()
    {
        while(true)
        {
            Debug.Log("ReplacePlane");

            rb.transform.position = RandomPointOnCircleEdge(100);
            Debug.Log("x" + rb.transform.position.x);
            Debug.Log("y" + rb.transform.position.y);

            yield return new WaitForSeconds(7);
        }

    }

    private Vector3 RandomPointOnCircleEdge(float radius)
    {
        var vector2 = UnityEngine.Random.insideUnitCircle.normalized * radius;
        return new Vector3(vector2.x + 350, 300, vector2.y + 300);
    }
}
