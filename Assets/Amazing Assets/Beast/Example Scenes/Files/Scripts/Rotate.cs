using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AmazingAssets.Beast.ExampleScripts
{
    public class Rotate : MonoBehaviour
    {
        public float speed = 1;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            transform.RotateAround(Vector3.zero, Vector3.up, speed * Time.deltaTime);

        }
    }

}