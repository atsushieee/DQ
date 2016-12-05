using UnityEngine;
using System;

public class GestureRecognitionComponent : MonoBehaviour {
	protected int state;

	void Start(){
	}

	void Update(){
	}

	public virtual void GestureRecornizer(GameObject bodyObject){
		
	}

    
    public int getState(){
		return state;
	}

	protected int nextState(int currentState) {
		return currentState + 1;
	}

	protected int initialState() {
		return 0;
	}

	protected double ComputeVelocityNorm(float[] currentPos, float[] previousPos) {
		return Math.Sqrt(
			Math.Pow(currentPos[0] - previousPos[0], 2) +
			Math.Pow(currentPos[1] - previousPos[1], 2) +
			Math.Pow(currentPos[2] - previousPos[2], 2)
		);
	}
}