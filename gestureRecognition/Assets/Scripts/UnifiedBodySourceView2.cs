using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Kinect = Windows.Kinect;

using System;
using System.Text;
using UnityOSC;

public class UnifiedBodySourceView2 : MonoBehaviour 
{
    public Material BoneMaterial;
    public GameObject BodySourceManager;
    
    private Dictionary<ulong, GameObject> _Bodies = new Dictionary<ulong, GameObject>();
    private BodySourceManager _BodyManager;

    // OSC Network Settings (only sending)
    public const string ADDRESS = "192.168.4.23";
    public const int PORT = 5000;

    // array for storing position
    private float[] currentWristPos = new float[3];
    private float[] previousWristPos = new float[3];

    private double currentVelocityNorm;
    private const int WIDTH_OF_BUFFER = 100;
    private double[] velocityBuffer = new double[WIDTH_OF_BUFFER];
    private const int WIDTH_OF_AVERAGE = 10;

    // for storing start position and end position
    private bool isMovingFast = false;
    private float[] startPos = new float[3];
    private float[] endPos = new float[3];

    public GameObject attackGestureCube;

    private Dictionary<Kinect.JointType, Kinect.JointType> _BoneMap = new Dictionary<Kinect.JointType, Kinect.JointType>()
    {
        { Kinect.JointType.FootLeft, Kinect.JointType.AnkleLeft },
        { Kinect.JointType.AnkleLeft, Kinect.JointType.KneeLeft },
        { Kinect.JointType.KneeLeft, Kinect.JointType.HipLeft },
        { Kinect.JointType.HipLeft, Kinect.JointType.SpineBase },
        
        { Kinect.JointType.FootRight, Kinect.JointType.AnkleRight },
        { Kinect.JointType.AnkleRight, Kinect.JointType.KneeRight },
        { Kinect.JointType.KneeRight, Kinect.JointType.HipRight },
        { Kinect.JointType.HipRight, Kinect.JointType.SpineBase },
        
        { Kinect.JointType.HandTipLeft, Kinect.JointType.HandLeft },
        { Kinect.JointType.ThumbLeft, Kinect.JointType.HandLeft },
        { Kinect.JointType.HandLeft, Kinect.JointType.WristLeft },
        { Kinect.JointType.WristLeft, Kinect.JointType.ElbowLeft },
        { Kinect.JointType.ElbowLeft, Kinect.JointType.ShoulderLeft },
        { Kinect.JointType.ShoulderLeft, Kinect.JointType.SpineShoulder },
        
        { Kinect.JointType.HandTipRight, Kinect.JointType.HandRight },
        { Kinect.JointType.ThumbRight, Kinect.JointType.HandRight },
        { Kinect.JointType.HandRight, Kinect.JointType.WristRight },
        { Kinect.JointType.WristRight, Kinect.JointType.ElbowRight },
        { Kinect.JointType.ElbowRight, Kinect.JointType.ShoulderRight },
        { Kinect.JointType.ShoulderRight, Kinect.JointType.SpineShoulder },
        
        { Kinect.JointType.SpineBase, Kinect.JointType.SpineMid },
        { Kinect.JointType.SpineMid, Kinect.JointType.SpineShoulder },
        { Kinect.JointType.SpineShoulder, Kinect.JointType.Neck },
        { Kinect.JointType.Neck, Kinect.JointType.Head },
    };

    /// OSC initial setting (only sending)
    void Start()
    {
        OSCHandler.Instance.Init(ADDRESS, PORT);
    }

    void Update () 
    {
        if (BodySourceManager == null)
        {
            return;
        }
        
        _BodyManager = BodySourceManager.GetComponent<BodySourceManager>();
        if (_BodyManager == null)
        {
            return;
        }
        
        Kinect.Body[] data = _BodyManager.GetData();
        if (data == null)
        {
            return;
        }

        
        List<ulong> trackedIds = new List<ulong>();
        foreach(var body in data)
        {
            if (body == null)
            {
                continue;
              }
                
            if(body.IsTracked)
            {
                trackedIds.Add (body.TrackingId);
            }
        }
        
        List<ulong> knownIds = new List<ulong>(_Bodies.Keys);
        
        // First delete untracked bodies
        foreach(ulong trackingId in knownIds)
        {
            if(!trackedIds.Contains(trackingId))
            {
                Destroy(_Bodies[trackingId]);
                _Bodies.Remove(trackingId);
            }
        }

        foreach(var body in data)
        {
            if (body == null)
            {
                continue;
            }
            
            if(body.IsTracked)
            {
                if(!_Bodies.ContainsKey(body.TrackingId))
                {
                    _Bodies[body.TrackingId] = CreateBodyObject(body.TrackingId);
                }
                
                RefreshBodyObject(body, _Bodies[body.TrackingId]);
            }
        }
    }
    
    private GameObject CreateBodyObject(ulong id)
    {
        GameObject body = new GameObject("Body:" + id);
        
        for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
        {
            GameObject jointObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            LineRenderer lr = jointObj.AddComponent<LineRenderer>();
            lr.SetVertexCount(2);
            lr.material = BoneMaterial;
            lr.SetWidth(0.05f, 0.05f);
            
            jointObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            jointObj.name = jt.ToString();
            jointObj.transform.parent = body.transform;
        }
        
        return body;
    }
    

    int state = 0;
    double count = 0;
    bool isGestureRecognized = false;

    private void RefreshBodyObject(Kinect.Body body, GameObject bodyObject)
    {
        Transform wristRightTransform = bodyObject.transform.FindChild("WristRight");
        Transform headTransform = bodyObject.transform.FindChild("Head");
        Transform spineBaseTransform = bodyObject.transform.FindChild("SpineMid");

        // 現在の座標と1フレーム前の座標を格納して、速度を求める
        currentWristPos[0] = wristRightTransform.position.x;
        currentWristPos[1] = wristRightTransform.position.y;
        currentWristPos[2] = wristRightTransform.position.z;
        currentVelocityNorm = ComputeVelocityNorm(currentWristPos, previousWristPos); 
        for(int i = 0; i < 3; i++)
        {
            previousWristPos[i] = currentWristPos[i];
        }        

        // 現在の速度をバッファの先頭に格納して、移動平均を求める
        for(int i = velocityBuffer.Length - 1; i > 0; i--)
        {
            velocityBuffer[i] = velocityBuffer[i - 1];
        }
        velocityBuffer[0] = currentVelocityNorm;
        double averagedVelocity = 0;
        for (int i = 0; i < WIDTH_OF_AVERAGE; i++) {
            averagedVelocity += velocityBuffer[i];
        }
        averagedVelocity /= WIDTH_OF_AVERAGE;

        // state management
        switch( state ) {
            case 0:
                isGestureRecognized = false;
                if(averagedVelocity < 0.3 && wristRightTransform.position.y > headTransform.position.y) {
                    count += 0.033;
                } else {
                    count = 0;
                }
                if (count > 2) {
                    state = nextState(state);
                }
                attackGestureCube.GetComponent<Renderer>().material.color = Color.black;
                break;
            case 1:
                //移動平均から求めた速度が閾値を超えた場合の処理
                if(averagedVelocity > 0.3) {
                    for(int i = 0; i < 3; i++) {
                        startPos[i] = currentWristPos[i];
                    }
                    state = nextState(state);
                } 
                if (averagedVelocity < 0.15 && wristRightTransform.position.y < headTransform.position.y) {
                    Debug.Log("state 1 -> state 0");
                    state = initialState();
                }
                count = 0;
                attackGestureCube.GetComponent<Renderer>().material.color = Color.yellow;
                break;
            case 2:
                if(averagedVelocity > 0.5) {
                    isGestureRecognized = true;
                }
                if(averagedVelocity < 0.15) {
                    for(int i = 0; i < 3; i++) {
                        endPos[i] = currentWristPos[i];
                    }
                    // start posとend posを結んだときの傾きを求める
                    double degree = Math.Atan2(endPos[1] - startPos[1], endPos[0] - startPos[0]) * 180.0 / Math.PI;
                    Debug.Log("角度" + degree);
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

                    Debug.Log("速さの閾値" + isGestureRecognized);

                }
                break;
            case 3:
                count += 0.033;
                if (count > 1.5) {
                    state = initialState();
                    count = 0;
                }
                break;
        }

        //var wristLeftVals = new List<float>() { wristRightTransform.position.y, shoulderTransform.position.y };
        //OSCHandler.Instance.SendMessageToClient("To MacOS", "/Data", wristLeftVals);

        for (Kinect.JointType jt = Kinect.JointType.SpineBase; jt <= Kinect.JointType.ThumbRight; jt++)
        {
            Kinect.Joint sourceJoint = body.Joints[jt];
            Kinect.Joint? targetJoint = null;

            if (_BoneMap.ContainsKey(jt))
            {
                targetJoint = body.Joints[_BoneMap[jt]];
            }

            Transform jointObj = bodyObject.transform.FindChild(jt.ToString());
            jointObj.localPosition = GetVector3FromJoint(sourceJoint);

            LineRenderer lr = jointObj.GetComponent<LineRenderer>();
            if (targetJoint.HasValue)
            {
                lr.SetPosition(0, jointObj.localPosition);
                lr.SetPosition(1, GetVector3FromJoint(targetJoint.Value));
                lr.SetColors(GetColorForState(sourceJoint.TrackingState), GetColorForState(targetJoint.Value.TrackingState));
            }
            else
            {
                lr.enabled = false;
            }
        }

    }
    
    private static Color GetColorForState(Kinect.TrackingState state)
    {
        switch (state)
        {
        case Kinect.TrackingState.Tracked:
            return Color.green;

        case Kinect.TrackingState.Inferred:
            return Color.red;

        default:
            return Color.black;
        }
    }
    
    private Vector3 GetVector3FromJoint(Kinect.Joint joint)
    {
        return new Vector3(joint.Position.X * 10, joint.Position.Y * 10, joint.Position.Z * 10);
    }

    private Vector3 GetVector3ForBase(Kinect.Joint joint, float x, float y)
    {
        return new Vector3(x, y, joint.Position.Z * 10);
    }

    private Vector3 GetVector3ForOther(Kinect.Joint joint, float[] locoDistances, Vector3 tempSpineBase, float[] basePoint)
    {
        Vector3 tempVertex = new Vector3(joint.Position.X * 10 + locoDistances[0], joint.Position.Y * 10 + locoDistances[1], joint.Position.Z * 10 + locoDistances[2]); 
        return new Vector3((tempVertex.x - tempSpineBase.x) + basePoint[0], (tempVertex.y - tempSpineBase.y) + basePoint[1], (tempVertex.z - tempSpineBase.z) + basePoint[2]);
        //return new Vector3(joint.Position.X * 10 + locoDistance, joint.Position.Y * 10, joint.Position.Z * 10);
    }

    private double ComputeVelocityNorm(float[] currentPos, float[] previousPos) {
        return Math.Sqrt(
            Math.Pow(currentPos[0] - previousPos[0], 2) +
            Math.Pow(currentPos[1] - previousPos[1], 2) +
            Math.Pow(currentPos[2] - previousPos[2], 2)
        );
    }

    private int nextState(int currentState) {
        return currentState + 1;
    }

    private int initialState() {
        return 0;
    }
}
