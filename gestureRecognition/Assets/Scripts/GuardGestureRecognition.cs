using UnityEngine;
using System.Collections;
using System;

public class GuardGestureRecognition : GestureRecognitionComponent {
    private const double THRESHOLD = 1.0;
    private double duration = 0.0;
    public double slope;

    public GameObject defensiveGestureSphere;

    public override void GestureRecornizer(GameObject bodyObject) {
        Transform wristLeftTransform = bodyObject.transform.FindChild("WristLeft");
        Transform elbowLeftTransform = bodyObject.transform.FindChild("ElbowLeft");
        Transform shoulderLeftTransform = bodyObject.transform.FindChild("ShoulderLeft");

        // 左手WristとElbowを結んだ線の傾きを求める
        slope = CalculateSlope(wristLeftTransform, elbowLeftTransform);

        // slopeが定めた範囲かつWristがShoulderの内側に入っている場合、カウントを増やす、そうでない場合はカウントをゼロに。
        if (slope < -75 && slope > -105 && wristLeftTransform.position.x > shoulderLeftTransform.position.x) {
            duration += Time.deltaTime;
        } else {
            duration = 0;
        }

        // カウント数がTHRESHOLD超えたら、該当オブジェクトの色を変える
        if (duration > THRESHOLD) {
            defensiveGestureSphere.GetComponent<Renderer>().material.color = Color.green;
        } else {
            defensiveGestureSphere.GetComponent<Renderer>().material.color = Color.black;
        }

    }

    public double CalculateSlope(Transform pointA, Transform pointB) {
        double degree = Math.Atan2(pointB.position.y - pointA.position.y, pointB.position.x - pointA.position.x) * 180.0 / Math.PI;
        return degree;
    }
}