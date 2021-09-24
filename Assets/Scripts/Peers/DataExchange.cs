using System.Collections;
using System.Collections.Generic;
using Unity.WebRTC;
using UnityEngine;

public class DataExchange : MonoBehaviour
{
    #region Public Fields
    public RTCDataChannel dataChannel;
    #endregion

    #region Editor Fields
    [SerializeField] private DataExchange m_remotePeer;
    #endregion

    #region Private Fields
    private RTCPeerConnection m_peerConnection;
    private DelegateOnIceConnectionChange onIceConnectionChange;
    private DelegateOnIceCandidate onIceCandidate;
    private DelegateOnMessage onDataChannelMessage;
    private DelegateOnOpen onDataChannelOpen;
    private DelegateOnClose onDataChannelClose;
    private DelegateOnDataChannel onDataChannel;
    #endregion

    #region MonoBehaviour

    private void Awake()
    {
        WebRTC.Initialize();
    }

    private void OnDestroy()
    {
        WebRTC.Dispose();
    }

    private void Start()
    {
        onIceConnectionChange = state => { OnIceConnectionChange(m_peerConnection, state); };
        onIceCandidate = candidate => { OnIceCandidate(m_peerConnection, candidate); };
        onDataChannel = channel =>
        {
            m_remotePeer.dataChannel = channel;
            m_remotePeer.dataChannel.OnMessage = onDataChannelMessage;
        };
        onDataChannelMessage = bytes => { Debug.Log(System.Text.Encoding.UTF8.GetString(bytes)); };
    }

    #endregion

    #region Private Methods
    private void OnIceConnectionChange(RTCPeerConnection pc, RTCIceConnectionState state)
    {
        switch (state)
        {
            case RTCIceConnectionState.New:
                Debug.Log($"peer IceConnectionState: New");
                break;
            case RTCIceConnectionState.Checking:
                Debug.Log($"peer IceConnectionState: Checking");
                break;
            case RTCIceConnectionState.Closed:
                Debug.Log($"peer IceConnectionState: Closed");
                break;
            case RTCIceConnectionState.Completed:
                Debug.Log($"peer IceConnectionState: Completed");
                break;
            case RTCIceConnectionState.Connected:
                Debug.Log($"peer IceConnectionState: Connected");
                break;
            case RTCIceConnectionState.Disconnected:
                Debug.Log($"peer IceConnectionState: Disconnected");
                break;
            case RTCIceConnectionState.Failed:
                Debug.Log($"peer IceConnectionState: Failed");
                break;
            case RTCIceConnectionState.Max:
                Debug.Log($"peer IceConnectionState: Max");
                break;
            default:
                break;
        }
    }

    private void OnIceCandidate(RTCPeerConnection pc, RTCIceCandidate candidate)
    {
        m_peerConnection.AddIceCandidate(candidate);
        Debug.Log($"peer ICE candidate:\n {candidate.Candidate}");
    }
    #endregion

    #region Private Functions
    private RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new RTCIceServer[]
        {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
        };

        return config;
    }
    #endregion
}
