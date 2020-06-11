using UnityEngine;
using System.Collections;
using System;

public class DelayToInvoke : MonoBehaviour
{

    public static IEnumerator DelayToInvokeDo(Action action, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        action();
    }
}