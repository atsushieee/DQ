using UnityEngine;
using System.Collections;
using System;

public class GestureManagerComponent : MonoBehaviour {
	public GestureRecognitionComponent[] gestureRecognitionComponents;

	// Use this for initialization
	void Start () {
		gestureRecognitionComponents = gameObject.GetComponents<GestureRecognitionComponent> ();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Run(GameObject bodyObject){
		for(int i=0, i_max=gestureRecognitionComponents.Length; i<i_max; i++){
			gestureRecognitionComponents [i].GestureRecornizer (bodyObject);
		}
	}
}
