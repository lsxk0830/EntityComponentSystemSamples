using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

namespace Samples.HelloNetcode
{
    public class RpcUi : MonoBehaviour
    {
        public InputField m_InputText;
        public Text m_ChatText;
        public Canvas m_Canvas;
        public Text m_UserPrefab;
        public RectTransform m_ChatContent;
        public ScrollRect m_ScrollRect;
        public GameObject m_ChatWindowPrefab;

        private int m_CurrentUserSlot = 0;
        private int m_UserSlotHorizontalSpace = -13;
        private int m_OwnUser = -1;
        List<Text> m_CachedUserTexts;

        void Start()
        {
            m_CachedUserTexts = m_Canvas.GetComponentsInChildren<Text>().ToList();
        }

        void Update()
        {
            if (m_OwnUser == -1 && ClientServerBootstrap.ClientWorld != null)
            {
                // 非瘦 client 永远是列表第一
                var connectionQuery = ClientServerBootstrap.ClientWorld.EntityManager
                    .CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
                var connectionIds = connectionQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);
                if (connectionIds.Length > 0)
                {
                    // Client 只有一个连接
                    m_OwnUser = connectionIds[0].Value;
                }
            }

            if (RpcUiData.Messages.Data.IsCreated && RpcUiData.Messages.Data.TryDequeue(out var message))
            {
                var chatText = Instantiate(m_ChatText, m_ChatContent.transform);
                // 将消息颜色设置为蓝色，以防这是我们自己的消息
                if (message.ConvertToString().StartsWith($"User {m_OwnUser}"))
                    chatText.text += $"<color=blue>{message}</color>\n";
                else
                    chatText.text += $"{message}\n";

                // 将聊天文本滚动到底部，以便您看到最新消息
                // （当文本不再适合内容空间时）
                Canvas.ForceUpdateCanvases();
                m_ScrollRect.verticalNormalizedPosition = 0f;
            }

            while (RpcUiData.Users.Data.IsCreated && RpcUiData.Users.Data.TryDequeue(out var user))
            {
                var userText = GetUserText();
                userText.text = $"User {user}";
                m_CurrentUserSlot++;

                // 将名称涂成蓝色，以防这是我们自己的用户
                if (user == m_OwnUser)
                    userText.color = Color.blue;
            }
        }

        Text GetUserText()
        {
            foreach (var text in m_CachedUserTexts)
            {
                if (!text.enabled)
                {
                    text.enabled = true;
                    return text;
                }
            }
            var newText = Instantiate(m_UserPrefab, m_Canvas.transform, false);
            newText.GetComponent<RectTransform>().anchoredPosition3D += new Vector3(0,m_CurrentUserSlot*m_UserSlotHorizontalSpace, 0);
            m_CachedUserTexts.Add(newText);
            return newText;
        }

        public void SendChatMessage()
        {
            SendRPC(ClientServerBootstrap.ClientWorld, m_InputText.text);
            // 消息发送后清除输入文本，然后将 UI 焦点放回到该文本上
            // 所以它准备好接受下一条消息
            m_InputText.text = "";
            m_InputText.Select();
            m_InputText.ActivateInputField();
        }

        void SendRPC(World world, string message, Entity targetEntity = default)
        {
            if (world == null || !world.IsCreated) return;
            var entity = world.EntityManager.CreateEntity();
            world.EntityManager.AddComponentData(entity, new ChatMessage() { Message = message});
            world.EntityManager.AddComponent<SendRpcCommandRequest>(entity);
            if (targetEntity != Entity.Null)
                world.EntityManager.SetComponentData(entity,
                    new SendRpcCommandRequest() { TargetConnection = targetEntity });
        }
    }
}
