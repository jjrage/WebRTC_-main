using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeRotate : MonoBehaviour
{
    [SerializeField] private float m_speed = 10;

    #region MonoBehaviour

    private void Update()
    {
        transform.RotateAround(transform.position, Vector3.up, Time.deltaTime * m_speed);
    }

    #endregion

}
