using BestHTTP.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;


namespace Receiver
{
    public class Receiver : MonoBehaviour
    {
        #region Private Fields
        private DelegateOnTrack onTrack;
        private Action<string> onIceCandidate;
        private Action onLogin;
        private Action onLogout;
        private Action<string> onOffer;
        private Action<string> onAnswer;
        private Action onAnswerSend;
        private Action onConnect;
        private Action onDisconnect;
        private Action onCommunicationCompleted;
        private Action onDisconnected;
        private Action onPeerCreated;

        private RTCStatsReport lastResult = null;
        private RTCOfferAnswerOptions m_offerOptions = new RTCOfferAnswerOptions
        {
            iceRestart = false
        };

        private RTCOfferAnswerOptions m_answerOptions = new RTCOfferAnswerOptions { iceRestart = false };
        private RTCPeerConnection m_peerConnection;
        private List<RTCRtpSender> m_streamSenders;
        private List<RTCRtpSender> m_streamReceivers;
        private MediaStream m_sendVideoStream;
        private MediaStream m_sendAudioStream;
        private MediaStream m_receiveStream;
        private bool m_videoUpdateStarted;
        private WebSocket m_webSocket;
        private string m_currentCallName;

        private List<VideoStreamTrack> videoStreamTrackList;
        private List<MediaStream> receiveStreamList;

        #endregion

        #region Editor Fields
        [SerializeField] private string m_serverUrl = "wss://localhost:8443";
        [SerializeField] private string m_userName = "Receiver";
        [SerializeField] private string m_targetUserName = "Sender";

        [SerializeField] private Button m_btnConnect;
        [SerializeField] private Button m_btnLogin;
        [SerializeField] private Button m_btnLogout;
        [SerializeField] private Button m_btnDisconnect;
        [SerializeField] private Button m_btnHangout;
        [SerializeField] private Button m_btnAnswer;
        [SerializeField] private Button m_btnDecline;
        [SerializeField] private RawImage m_receiveImage;
        [SerializeField] private TMP_InputField m_textServerUrl;
        [SerializeField] private TMP_InputField m_textUserName;
        [SerializeField] private TMP_InputField m_textTargetUserName;
        [SerializeField] private GameObject m_receiveContainer;
        #endregion

        #region MonoBehaviour

        private void OnEnable()
        {
            m_streamSenders = new List<RTCRtpSender>();
            m_streamReceivers = new List<RTCRtpSender>();

            m_btnConnect.onClick.AddListener(Connect);
            m_btnLogin.onClick.AddListener(Login);
            m_btnLogout.onClick.AddListener(Logout);
            m_btnDisconnect.onClick.AddListener(Disconnect);
            m_btnAnswer.onClick.AddListener(AnswerCall);
            m_btnDecline.onClick.AddListener(DeclineCall);
            m_btnHangout.onClick.AddListener(HangOut);
            m_textServerUrl.text = m_serverUrl;
            m_textUserName.text = m_userName;
            m_textTargetUserName.text = m_targetUserName;

            //Receiving
            onTrack = e =>
            {
                m_receiveStream.AddTrack(e.Track);
            };

            onLogin += OnLogin;
            onLogout += OnLogout;
            onConnect += OnConnect;
            onDisconnect += OnDisconnect;
            onOffer = OnOffer;
            onAnswer = OnAnswer;
            onIceCandidate = OnIceCandidateHandler;
            onCommunicationCompleted += OnCommunicationCompleted;
            onDisconnected += OnDisconnected;
            onPeerCreated += AddTracks;
        }

        private void Awake()
        {
            WebRTC.Initialize(EncoderType.Software);
        }

        private void Start()
        {
            InitReceiveTracks();
            CreateLocalPeer();
        }

        private void OnDisable()
        {
            m_btnConnect.onClick.RemoveListener(Connect);
            m_btnLogin.onClick.RemoveListener(Login);
            m_btnLogout.onClick.RemoveListener(Logout);
            m_btnDisconnect.onClick.RemoveListener(Disconnect);

            if (m_webSocket != null)
            {
                m_webSocket.OnOpen -= OnOpen;
                m_webSocket.OnMessage -= OnMessageReceived;
                m_webSocket.OnClosed -= OnClosed;
                m_webSocket.OnError -= OnError;
            }
        }

        private void OnDestroy()
        {
            WebRTC.Dispose();
        }

        #endregion

        #region Private Methods

        private void OnIceConnectionChange(RTCIceConnectionState state)
        {
            switch (state)
            {
                case RTCIceConnectionState.New:
                    Debug.Log($"IceConnectionState: New");
                    break;
                case RTCIceConnectionState.Checking:
                    Debug.Log($"IceConnectionState: Checking");
                    break;
                case RTCIceConnectionState.Closed:
                    Debug.Log($"IceConnectionState: Closed");
                    break;
                case RTCIceConnectionState.Completed:
                    Debug.Log($"IceConnectionState: Completed");
                    break;
                case RTCIceConnectionState.Connected:
                    Debug.Log($"IceConnectionState: Connected");
                    onCommunicationCompleted?.Invoke();
                    break;
                case RTCIceConnectionState.Disconnected:
                    Debug.Log($"IceConnectionState: Disconnected");
                    onDisconnected?.Invoke();
                    break;
                case RTCIceConnectionState.Failed:
                    Debug.Log($"IceConnectionState: Failed");
                    break;
                case RTCIceConnectionState.Max:
                    Debug.Log($"IceConnectionState: Max");
                    break;
                default:
                    break;
            }
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            Debug.Log($" ICE candidate:\n {candidate.Candidate}");
        }

        private void AddTracks()
        {
            if (!m_videoUpdateStarted)
            {
                Debug.Log("Updaye video");
                StartCoroutine(WebRTC.Update());
                m_videoUpdateStarted = true;
            }
        }

        private void RemoveTracks()
        {
            foreach (var sender in m_streamSenders)
            {
                m_peerConnection.RemoveTrack(sender);
            }
            foreach (var sender in m_streamReceivers)
            {
                m_peerConnection.RemoveTrack(sender);
            }
            m_streamSenders.Clear();
            m_streamReceivers.Clear();
        }

        private void OnPeerCreated()
        {
            AddTracks();
        }

        private void InitReceiveTracks()
        {
            m_receiveStream = new MediaStream();
            m_receiveStream.OnAddTrack = e =>
            {
                m_receiveContainer.gameObject.SetActive(true);
                Debug.Log($"onAddTrack {e.Track.Kind.ToString()}");
                if (e.Track is VideoStreamTrack videoTrack)
                {
                    m_receiveImage.texture = videoTrack.InitializeReceiver(1280, 720);
                }

                if (e.Track is AudioStreamTrack audioTrack)
                {
                    //todo init audio
                }
            };
        }

        private void CreateLocalPeer()
        {
            Debug.Log("Creating local peer");
            var configuration = GetSelectedSdpSemantics();
            m_peerConnection = new RTCPeerConnection(ref configuration);
            m_peerConnection.OnIceConnectionChange =
                new DelegateOnIceConnectionChange(state => OnIceConnectionChange(state));
            m_peerConnection.OnIceCandidate = new DelegateOnIceCandidate(candidate => OnIceCandidate(candidate));
            m_peerConnection.OnNegotiationNeeded = OnNegotioationNeeded;
            m_peerConnection.OnTrack = onTrack;
            onPeerCreated?.Invoke();
        }

        private void OnNegotioationNeeded()
        {
            Debug.Log("OnNegotioationNeeded");
            StartCoroutine(PeerNegotiationNeeded(m_peerConnection));

        }
        private static void OnCreateSessionDescriptionError(RTCError error)
        {
            Debug.LogError($"Error Detail Type: {error.message}");
        }

        private void OnSetLocalSuccess(RTCPeerConnection pc)
        {
            Debug.Log($"SetLocalDescription complete");
        }
        static void OnSetSessionDescriptionError(ref RTCError error)
        {
            Debug.LogError($"Error Detail Type: {error.message}");
        }

        private void OnIceCandidateHandler(string message)
        {
            CandidateData cd = JsonUtility.FromJson<CandidateData>(message);
            Debug.Log("Candidate");
            RTCIceCandidateInit init = new RTCIceCandidateInit() { candidate = cd.candidate.candidate, sdpMid = cd.candidate.sdpMid, sdpMLineIndex = cd.candidate.sdpMLineIndex };
            m_peerConnection.AddIceCandidate(new RTCIceCandidate(init));
        }

        private void OnAnswer(string message)
        {
            Debug.Log($"Answer: {message}");
            Answer answer = JsonUtility.FromJson<Answer>(message);
            RTCSessionDescription session = new RTCSessionDescription();
            session.type = RTCSdpType.Answer; ;
            session.sdp = answer.answer.sdp;
            m_peerConnection.SetRemoteDescription(ref session);
        }

        private void HandleOffer(string offerObj)
        {
            Offer offer = JsonUtility.FromJson<Offer>(offerObj);
            m_currentCallName = offer.name;
            RTCSessionDescription sessionFromOffer = new RTCSessionDescription();
            sessionFromOffer.type = RTCSdpType.Offer;
            sessionFromOffer.sdp = offer.offer.sdp;
            Debug.Log($"peer state = {m_peerConnection.SignalingState}");
            m_peerConnection.SetRemoteDescription(ref sessionFromOffer);
            Debug.Log($"peer state = {m_peerConnection.SignalingState}");
        }

        private void OnDisconnected()
        {
            HangOut();
        }

        private void UpdateStatsPacketSize(RTCStatsReport res)
        {
            foreach (RTCStats stats in res.Stats.Values)
            {
                if (!(stats is RTCOutboundRTPStreamStats report))
                {
                    continue;
                }

                if (report.isRemote)
                    return;

                long now = report.Timestamp;
                ulong bytes = report.bytesSent;

                if (lastResult != null)
                {
                    if (!lastResult.TryGetValue(report.Id, out RTCStats last))
                        continue;

                    var lastStats = last as RTCOutboundRTPStreamStats;
                    var duration = (double)(now - lastStats.Timestamp) / 1000000;
                    ulong bitrate = (ulong)(8 * (bytes - lastStats.bytesSent) / duration);
                    Debug.Log($"bitrate:{bitrate}");
                }

            }
            lastResult = res;
        }

        private void OnCommunicationCompleted()
        {
            m_btnAnswer.gameObject.SetActive(false);
            m_btnDecline.gameObject.SetActive(false);
            m_btnHangout.gameObject.SetActive(true);
            m_textTargetUserName.interactable = false;
            StartCoroutine(UpdateStats());
        }

        private void HangOut()
        {
            SendLeave();
            RemoveTracks();
            m_videoUpdateStarted = false;
            m_peerConnection.Close();
            m_peerConnection = null;
            m_receiveImage.texture = null;
            m_receiveContainer.SetActive(false);
            m_btnHangout.gameObject.SetActive(false);
            m_textTargetUserName.interactable = true;
            CreateLocalPeer();
        }

        private void DeclineCall()
        {
            m_btnAnswer.gameObject.SetActive(false);
            m_btnDecline.gameObject.SetActive(false);
        }

        private void OnOffer(string obj)
        {
            HandleOffer(obj);
            m_btnAnswer.gameObject.SetActive(true);
            m_btnDecline.gameObject.SetActive(true);
        }

        private void AnswerCall()
        {
            if (m_peerConnection == null)
            {
                CreateLocalPeer();
            }
            StartCoroutine(SendAnswer(m_currentCallName));
        }

        private void OnLogout()
        {
            m_textUserName.interactable = true;
            m_btnLogin.gameObject.SetActive(true);
            m_btnLogout.gameObject.SetActive(false);
            m_textTargetUserName.interactable = false;
        }

        private void OnLogin()
        {
            Debug.Log("Login Success");
            m_textUserName.interactable = false;
            m_btnLogin.gameObject.SetActive(false);
            m_btnLogout.gameObject.SetActive(true);
            m_textTargetUserName.interactable = true;
        }

        private void OnError(WebSocket websocket, string reason)
        {
            Debug.Log($"OnError: {reason}");
            Logout();
            Disconnect();
        }

        private void OnClosed(WebSocket websocket, ushort code, string message)
        {
            Debug.Log("OnClose");
            onDisconnect?.Invoke();
        }

        private void OnMessageReceived(WebSocket websocket, string message)
        {

            MessageType mt = JsonUtility.FromJson<MessageType>(message);
            switch (mt.type)
            {
                case "login":
                    onLogin?.Invoke();
                    LoginAnswer la = JsonUtility.FromJson<LoginAnswer>(message);
                    break;
                case "leave":
                    //onLogout?.Invoke();
                    Debug.Log("Leave data");
                    break;
                case "offer":
                    onOffer?.Invoke(message);
                    break;
                case "answer":
                    onAnswer?.Invoke(message);
                    break;
                case "candidate":
                    onIceCandidate?.Invoke(message);
                    break;
                default:
                    Debug.Log($"OnMessageReceived: {message}");
                    break;
            }
        }

        private void OnOpen(WebSocket websocket)
        {
            Debug.Log($"Connected to {m_textServerUrl.text}");
            onConnect?.Invoke();
        }

        private void OnDisconnect()
        {
            m_textServerUrl.interactable = true;
            m_btnConnect.gameObject.SetActive(true);
            m_btnDisconnect.gameObject.SetActive(false);
            m_textUserName.interactable = false;
            m_btnLogin.interactable = false;
        }

        private void OnConnect()
        {
            m_textServerUrl.interactable = false;
            m_btnConnect.gameObject.SetActive(false);
            m_btnDisconnect.gameObject.SetActive(true);
            m_textUserName.interactable = true;
            m_btnLogin.interactable = true;
        }

        private void Login()
        {
            string json = JsonUtility.ToJson(new Login() { type = "login", name = m_textUserName.text });
            m_webSocket.Send(json);
        }
        private void Logout()
        {
            m_textTargetUserName.interactable = false;
            m_btnHangout.gameObject.SetActive(false);
            SendLeave();
            RemoveTracks();
        }

        private void SendLeave()
        {
            string json = JsonUtility.ToJson(new Login() { type = "leave", name = m_textUserName.text });
            m_webSocket.Send(json);
            StopAllCoroutines();

        }

        private void Connect()
        {
            m_webSocket = new WebSocket(new Uri(m_textServerUrl.text));
            m_webSocket.OnOpen += OnOpen;
            m_webSocket.OnMessage += OnMessageReceived;
            m_webSocket.OnClosed += OnClosed;
            m_webSocket.OnError += OnError;
            m_webSocket.Open();
        }

        private void Disconnect()
        {
            m_webSocket.Close();
        }

        private void OfferToUser()
        {
            StartCoroutine(SendOffer());
        }
        #endregion

        #region Coroutines

        IEnumerator PeerNegotiationNeeded(RTCPeerConnection pc)
        {
            Debug.Log($"createOffer start");
            var op = pc.CreateOffer();
            yield return op;
            if (!op.IsError)
            {
                if (pc.SignalingState != RTCSignalingState.Stable)
                {
                    Debug.LogError($"signaling state is not stable.");
                    yield break;
                }

                yield return StartCoroutine(OnCreateOfferSuccess(pc, op.Desc));
            }
            else
            {
                OnCreateSessionDescriptionError(op.Error);
            }
        }

        private IEnumerator OnCreateOfferSuccess(RTCPeerConnection pc, RTCSessionDescription desc)
        {
            Debug.Log($"Offer from");
            Debug.Log($"setLocalDescription start");
            var op = pc.SetLocalDescription(ref desc);
            yield return op;

            if (!op.IsError)
            {
                OnSetLocalSuccess(pc);
            }
            else
            {
                var error = op.Error;
                OnSetSessionDescriptionError(ref error);
            }
        }

        private IEnumerator UpdateStats()
        {
            while (true)
            {
                RTCRtpSender sender = m_peerConnection.GetSenders().First();
                RTCStatsReportAsyncOperation op = sender.GetStats();
                yield return op;
                if (op.IsError)
                {
                    Debug.LogErrorFormat("RTCRtpSender.GetStats() is failed {0}", op.Error.errorType);
                }
                else
                {
                    UpdateStatsPacketSize(op.Value);
                }

                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator SendOffer()
        {
            if (string.IsNullOrEmpty(m_textTargetUserName.text))
            {
                Debug.LogError("Caller's name is empty");
                yield break;
            }
            Offer offer = new Offer();
            offer.type = "offer";
            offer.name = m_textTargetUserName.text;
            offer.offer = new SdpType();

            var createdOffer = m_peerConnection.CreateOffer(ref m_offerOptions);
            yield return createdOffer;
            Debug.Log($"Creating offer for {m_textTargetUserName.text}");
            if (createdOffer.IsError)
            {
                Debug.Log(createdOffer.Error.message);
            }
            else
            {
                RTCSessionDescription session = new RTCSessionDescription();
                session.type = RTCSdpType.Offer;
                session.sdp = createdOffer.Desc.sdp;
                m_peerConnection.SetLocalDescription(ref session);
                offer.offer.type = "offer";
                offer.offer.sdp = createdOffer.Desc.sdp;
                string json = JsonUtility.ToJson(offer);
                m_webSocket.Send(json);
            }
        }

        private IEnumerator SendAnswer(string name)
        {
            Answer answer = new Answer();
            answer.type = "answer";
            answer.name = name;
            answer.answer = new SdpType();

            RTCSessionDescriptionAsyncOperation createAnswer = m_peerConnection.CreateAnswer(ref m_answerOptions);
            yield return createAnswer;
            if (createAnswer.IsError)
            {
                Debug.Log(createAnswer.Error.message);
            }
            else
            {
                RTCSessionDescription session = new RTCSessionDescription();
                session.type = RTCSdpType.Answer;
                session.sdp = createAnswer.Desc.sdp;
                m_peerConnection.SetLocalDescription(ref session);
                answer.answer.sdp = createAnswer.Desc.sdp;
                answer.answer.type = "answer";
                string json = JsonUtility.ToJson(answer);
                m_webSocket.Send(json);
                onAnswerSend?.Invoke();
            }
        }
        #endregion

        RTCConfiguration GetSelectedSdpSemantics()
        {
            RTCConfiguration config = default;
            config.iceServers = new RTCIceServer[]
            {
            new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
            };

            return config;
        }

    }

    public class MessageType
    {
        public string type;
    }

    public class Login
    {
        public string type;
        public string name;
    }
    [Serializable]
    public class LoginAnswer
    {
        public string type;
        public string success;
        public string[] allUsers;
    }
    [Serializable]
    public class Offer
    {
        public string type;
        public SdpType offer;
        public string name;
    }
    [Serializable]
    public class Answer
    {
        public string name;
        public string type;
        public SdpType answer;
    }

    [Serializable]
    public class SdpType
    {
        public string type;
        public string sdp;
    }
    [Serializable]
    public class CandidateData
    {
        public CandidateDitails candidate;
        public string type;
    }
    [Serializable]
    public class CandidateDitails
    {
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }
}