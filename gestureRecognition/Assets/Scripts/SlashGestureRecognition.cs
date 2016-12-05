using UnityEngine;
using System.Collections;
using System;

public class SlashGestureRecognition : GestureRecognitionComponent
{
	// array for storing position
	public float[] currentWristPos = new float[3];
	public float[] previousWristPos = new float[3];

	public double currentVelocityNorm;
	private const int WIDTH_OF_BUFFER = 100;
	private double[] velocityBuffer = new double[WIDTH_OF_BUFFER];
	private const int WIDTH_OF_AVERAGE = 10;

	// for storing start position and end position
	public bool isMovingFast = false;
	public float[] startPos = new float[3];
	public float[] endPos = new float[3];
	public double averageDiff;

	public int state = 0;
	private double duration = 0;
	private double errorDuration = 0;
	private bool isGestureRecognized = false;
	// 移動ポイントを格納する動的配列を作成
	private ArrayList pointsX = new ArrayList();
	private ArrayList pointsY = new ArrayList();

	public GameObject attackGestureCube;
	
	public override void GestureRecornizer(GameObject bodyObject){
		Transform wristRightTransform = bodyObject.transform.FindChild("WristRight");
		Transform headTransform = bodyObject.transform.FindChild("Head");
		Transform spineBaseTransform = bodyObject.transform.FindChild("SpineMid");

        // 現在の座標と1フレーム前の座標を格納して、速度を求める
        WristPosUpdate(wristRightTransform);

        // 現在の速度をバッファの先頭に格納して、移動平均を求める
        double averagedVelocity = MovingAverage();

		// state management
		switch( state ) {
		case 0:
			errorDuration = 0;
			averageDiff = 0;
			isGestureRecognized = false;
			if(averagedVelocity < 0.2 && wristRightTransform.position.y > headTransform.position.y) {
				duration += Time.deltaTime;
			} else {
				duration = 0;
			}
			if (duration > 1) {
				state = nextState(state);
			}
			attackGestureCube.GetComponent<Renderer>().material.color = Color.black;
			break;
		case 1:
			//移動平均から求めた速度が閾値を超えた場合の処理
			if(averagedVelocity > 0.2) {
				for(int i = 0; i < 3; i++) {
					startPos[i] = currentWristPos[i];
				}
				pointsX.Add(startPos[0]);
				pointsY.Add(startPos[1]);

				state = nextState(state);
			} 
			if (wristRightTransform.position.y < headTransform.position.y) {
				errorDuration += Time.deltaTime;
				if (errorDuration > 1) {
					Debug.Log("state 1 -> state 0");
					state = initialState();
				}
			}
			duration = 0;
			attackGestureCube.GetComponent<Renderer>().material.color = Color.yellow;
			break;
		case 2:
			errorDuration = 0;
			// 現在のポジションを動的配列に格納
			pointsX.Add(currentWristPos[0]);
			pointsY.Add(currentWristPos[1]);

			if(averagedVelocity > 0.5) {
				isGestureRecognized = true;
			}
			if(averagedVelocity < 0.1) {
				for(int i = 0; i < 3; i++) {
					endPos[i] = currentWristPos[i];
				}

				Debug.Log(pointsX.Count);
				// 始点と終点を結んだ直線と軌道の違いを求める

				double a;
				double b;
				a = (endPos[1] - startPos[1]) / (endPos[0] - startPos[0]);
				b = endPos[1] - a * endPos[0];
				double[] distance = new double[pointsX.Count];

				//ObejctクラスからDoubleクラスに変更
				Double[] pointXValues = (Double[])pointsX.ToArray(typeof(Double)); 
				Double[] pointYValues = (Double[])pointsY.ToArray(typeof(Double));


				for (int i = 0; i < pointsX.Count; i++)
				{
					double numerator = Math.Abs (a * (double)pointXValues [i] - (double)pointYValues [i] + b);
					double denominatro = Math.Sqrt (Math.Pow (a, 2) + Math.Pow (b, 2));

					distance[i] =  numerator/ denominatro;
					averageDiff += distance[i];
				}
				// distance配列の平均誤差を出す
				averageDiff /= distance.Length;
                Debug.Log("Diff:" + averageDiff);
                // averageDiffが閾値よりも小さければ、フラグ立てる。




                // pointsX, pointsYの動的配列を空にする
                pointsX.Clear();
				pointsY.Clear();

				Debug.Log(pointsX.Count);

				// start posとend posを結んだときの傾きを求める
				double degree = Math.Atan2(endPos[1] - startPos[1], endPos[0] - startPos[0]) * 180.0 / Math.PI;
				Debug.Log("角度: " + degree);
				//OSCHandler.Instance.SendMessageToClient("To MacOS", "/Data", degree);

				if (isGestureRecognized && wristRightTransform.position.y < spineBaseTransform.position.y) {         
					if (degree < -105 && degree > -150)
					{
						attackGestureCube.GetComponent<Renderer>().material.color = Color.red;
						state = nextState(state);
					}
					else if (degree < -75 && degree > -105)
					{
						attackGestureCube.GetComponent<Renderer>().material.color = Color.green;
						state = nextState(state);
					}
					else if (degree < -30 && degree > -75)
					{
						attackGestureCube.GetComponent<Renderer>().material.color = Color.blue;
						state = nextState(state);
					} else {
						state = initialState();
					}
				} else {
					state = initialState();
				}

				Debug.Log("速さの閾値: " + isGestureRecognized);
			}
			break;
		case 3:
			duration += Time.deltaTime;
			if (duration > 1.0) {
				state = initialState();
				duration = 0;
			}
			break;
		}
	}

    public void WristPosUpdate(Transform wristRightTransform) {
        currentWristPos[0] = wristRightTransform.position.x;
        currentWristPos[1] = wristRightTransform.position.y;
        currentWristPos[2] = wristRightTransform.position.z;
        currentVelocityNorm = ComputeVelocityNorm(currentWristPos, previousWristPos);
        for (int i = 0; i < 3; i++)
        {
            previousWristPos[i] = currentWristPos[i];
        }
    }

    public double MovingAverage() {
        for (int i = velocityBuffer.Length - 1; i > 0; i--)
        {
            velocityBuffer[i] = velocityBuffer[i - 1];
        }
        velocityBuffer[0] = currentVelocityNorm;
        double averagedVelocity = 0;
        for (int i = 0; i < WIDTH_OF_AVERAGE; i++)
        {
            averagedVelocity += velocityBuffer[i];
        }
        return averagedVelocity /= WIDTH_OF_AVERAGE;
    } 
}
