using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Webcam : MonoBehaviour
{
    [SerializeField] private RawImage m_image;

    #region MonoBehaviour

    void Start()
    {
        WebCamTexture webcamTexture = new WebCamTexture();
        m_image.gameObject.SetActive(true);
        m_image.texture = webcamTexture;
        webcamTexture.Play();
    }
    #endregion
}
