using UnityEngine;

class CameraRoll : MonoBehaviour
{
    public Transform Target; //获取旋转目标
    public int Speed;

    void Update()
    {
        transform.RotateAround(Target.position, Vector3.up, Speed * Time.deltaTime); //摄像机围绕目标旋转
    }
}
