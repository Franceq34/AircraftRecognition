using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VelocityManager : MonoBehaviour {

    public Vector3 move = new Vector3(0, 0, 0);
    public Rigidbody rb;

    // Use this for initialization
	void Start () {
        rb = GetComponent<Rigidbody>();
        rb.velocity = move;
    }

    private void FixedUpdate()
    {
    }
}
