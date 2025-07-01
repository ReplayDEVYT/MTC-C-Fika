using System;
using System.Collections;
using UnityEngine;

public class RespawnHelper : MonoBehaviour
{
    public static void DelayedAction(Action action, float delay)
    {
        var go = new GameObject("RespawnHelper");
        var helper = go.AddComponent<RespawnHelper>();
        helper.StartCoroutine(helper.Run(action, delay));
    }

    private IEnumerator Run(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action();
        Destroy(gameObject);
    }
}