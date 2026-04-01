using UnityEngine;

#if EOS_INSTALLED
using PlayEveryWare.EpicOnlineServices;
using Epic.OnlineServices;
using Epic.OnlineServices.Connect;
#endif

/// <summary>
/// Simplified EOS Manager that works with the official PlayEveryWare EOS plugin
/// Handles authentication and provides ProductUserId for matchmaking
/// </summary>
public class SimpleEOSManager : MonoBehaviour
{
    public static SimpleEOSManager Instance { get; private set; }
    
    [Header("Status")]
    public bool isAuthenticated = false;
    
#if EOS_INSTALLED
    public ProductUserId localUserId;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // Wait for EOS plugin to initialize, then login
        StartCoroutine(WaitForEOSAndLogin());
    }
    
    private System.Collections.IEnumerator WaitForEOSAndLogin()
    {
        // Wait for EOS plugin to initialize
        while (EOSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        // Wait for platform interface to be ready
        var eosManager = PlayEveryWare.EpicOnlineServices.EOSManager.Instance;
        while (eosManager.GetEOSPlatformInterface() == null)
        {
            Debug.Log("Waiting for EOS Platform Interface to initialize...");
            yield return new WaitForSeconds(0.5f);
        }
        
        Debug.Log("EOS Plugin initialized, logging in anonymously...");
        LoginAnonymous();
    }
    
    /// <summary>
    /// Login anonymously using device ID
    /// </summary>
    private void LoginAnonymous()
    {
        var eosManager = PlayEveryWare.EpicOnlineServices.EOSManager.Instance;
        var platformInterface = eosManager.GetEOSPlatformInterface();
        if (platformInterface == null)
        {
            Debug.LogError("Failed to get EOS Platform Interface");
            return;
        }
        var connectInterface = platformInterface.GetConnectInterface();
        
        // Try to login directly first (device ID may already exist)
        var loginOptions = new LoginOptions()
        {
            Credentials = new Credentials()
            {
                Type = ExternalCredentialType.DeviceidAccessToken,
                Token = null
            },
            UserLoginInfo = new UserLoginInfo()
            {
                DisplayName = "Player"
            }
        };
        
        connectInterface.Login(ref loginOptions, null, OnConnectLogin);
    }
    
    private void OnCreateDeviceId(ref CreateDeviceIdCallbackInfo data)
    {
        if (data.ResultCode == Result.Success || data.ResultCode == Result.DuplicateNotAllowed)
        {
            Debug.Log("Device ID created/exists, logging in...");
            
            var eosManager = PlayEveryWare.EpicOnlineServices.EOSManager.Instance;
            var platformInterface = eosManager.GetEOSPlatformInterface();
            var connectInterface = platformInterface.GetConnectInterface();
            
            var loginOptions = new LoginOptions()
            {
                Credentials = new Credentials()
                {
                    Type = ExternalCredentialType.DeviceidAccessToken,
                    Token = null
                },
                UserLoginInfo = new UserLoginInfo()
                {
                    DisplayName = "Player"
                }
            };
            
            connectInterface.Login(ref loginOptions, null, OnConnectLogin);
        }
        else
        {
            Debug.LogError($"Failed to create device ID: {data.ResultCode}");
        }
    }
    
    private void OnConnectLogin(ref LoginCallbackInfo data)
    {
        if (data.ResultCode == Result.Success)
        {
            localUserId = data.LocalUserId;
            isAuthenticated = true;
            
            Debug.Log($"EOS anonymous login successful! ProductUserId: {GetLocalUserIdString()}");
        }
        else if (data.ResultCode == Result.NotFound || data.ResultCode == Result.InvalidUser)
        {
            // Device ID doesn't exist yet, create it first
            Debug.Log("Device ID not found, creating new device ID...");
            CreateDeviceId();
        }
        else
        {
            Debug.LogError($"EOS login failed: {data.ResultCode}");
        }
    }
    
    private void CreateDeviceId()
    {
        var eosManager = PlayEveryWare.EpicOnlineServices.EOSManager.Instance;
        var platformInterface = eosManager.GetEOSPlatformInterface();
        var connectInterface = platformInterface.GetConnectInterface();
        
        var createDeviceIdOptions = new CreateDeviceIdOptions()
        {
            DeviceModel = SystemInfo.deviceModel
        };
        
        connectInterface.CreateDeviceId(ref createDeviceIdOptions, null, OnCreateDeviceId);
    }
    
    /// <summary>
    /// Get local Product User ID as string (for Firebase storage)
    /// </summary>
    public string GetLocalUserIdString()
    {
        if (localUserId == null) return null;
        
        localUserId.ToString(out Utf8String outBuffer);
        return outBuffer;
    }
    
    /// <summary>
    /// Get the EOS Platform Interface
    /// </summary>
    public Epic.OnlineServices.Platform.PlatformInterface GetPlatformInterface()
    {
        return PlayEveryWare.EpicOnlineServices.EOSManager.Instance.GetEOSPlatformInterface();
    }
#else
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        
        Debug.LogWarning("EOS SDK not installed. Multiplayer will use fallback connection method.");
    }
    
    public string GetLocalUserIdString()
    {
        return null;
    }
#endif
}
