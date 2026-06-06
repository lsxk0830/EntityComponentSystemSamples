using System;
using System.Collections;
using Unity.Collections;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Samples.HelloNetcode
{
    public class PlayerAuthentication : MonoBehaviour
    {
        void Start()
        {
            if (!ConnectionApprovalData.PlayerAuthenticationEnabled.Data)
                return;

            // 我们只会在启用了身份验证的情况下通过 client worlds 到达此处，因此永远不会在 servers 上完成此操作
            SignIn();
        }

        async void SignIn()
        {
            try
            {
                await UnityServices.InitializeAsync();

                AuthenticationService.Instance.SignedIn += () =>
                {
                    Debug.Log("Sign in anonymously succeeded!");

                    ConnectionApprovalData.ApprovalPayload.Data.Payload.Append(AuthenticationService.Instance.PlayerId);
                    ConnectionApprovalData.ApprovalPayload.Data.Payload.Append(':');
                    ConnectionApprovalData.ApprovalPayload.Data.Payload.Append(AuthenticationService.Instance
                        .AccessToken);
                };

                AuthenticationService.Instance.SignInFailed += errorResponse =>
                {
                    Debug.LogError($"Sign in anonymously failed with error code: {errorResponse.ErrorCode}");
                };

                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception when trying to sign in: {e.Message}");
            }
        }

        IEnumerator GetPlayerInfo(PendingApproval pendingApproval)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get($"https://player-auth.services.api.unity.com/v1/users/{pendingApproval.PlayerId}"))
            {
                webRequest.SetRequestHeader("ProjectId", Application.cloudProjectId);
                webRequest.SetRequestHeader("Authorization", $"Bearer {pendingApproval.AccessToken}");

                yield return webRequest.SendWebRequest();

                bool success = false;
                switch (webRequest.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                        Debug.LogError($"GetPlayerInfo ConnectionError: {webRequest.error}");
                        break;
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogError($"GetPlayerInfo DataProcessingError: {webRequest.error}");
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogError($"GetPlayerInfo ProtocolError: {webRequest.error}");
                        break;
                    case UnityWebRequest.Result.Success:
                        success = true;
                        break;
                }

                ConnectionApprovalData.ApprovalResults.Data.Enqueue(new ApprovalResult(){ Success = success, Payload = pendingApproval.Payload, ConnectionEntity = pendingApproval.ConnectionEntity});
            }
        }

        void Update()
        {
            // NOTE: 这将获取所有排队的玩家批准请求，但一次最多仅适用于 15 名玩家
            // 将达到此特定服务速率限制。清除限制后需要批量获取其余部分。
            // 有关详细信息，请参阅 https://services.docs.unity.com/player-auth/v1/。
            while (ConnectionApprovalData.PendingApprovals.Data.IsCreated && ConnectionApprovalData.PendingApprovals.Data.TryDequeue(out var pendingApproval))
            {
                StartCoroutine(GetPlayerInfo(pendingApproval));
            }
        }
    }
}
