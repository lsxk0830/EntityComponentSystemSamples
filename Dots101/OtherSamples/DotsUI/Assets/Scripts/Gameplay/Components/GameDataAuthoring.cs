using Unity.Entities;
using UnityEngine;

namespace Unity.DotsUISample
{
    public class GameDataAuthoring : MonoBehaviour
    {
        public DialogueData startDialogue;
        public DialogueData endDialogue;
        public QuestData quest;
        public CollectablesData collectables;

        class Baker : Baker<GameDataAuthoring>
        {
            public override void Bake(GameDataAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.None);

                // 为了避免改变原始的可编写脚本的对象，我们制作副本
                AddComponent(entity, new GameData
                {
                    StartDialogue = Instantiate(authoring.startDialogue),
                    EndDialogue = Instantiate(authoring.endDialogue),
                    Quest = Instantiate(authoring.quest),
                    Collectables = Instantiate(authoring.collectables),
                    State = GameState.Init,
                    InterfaceState = InterfaceState.Questing,
                });
            }
        }
    }

    public struct GameData : IComponentData
    {
        public GameState State;
        public InterfaceState InterfaceState;
        public UnityObjectRef<QuestData> Quest;
        public UnityObjectRef<CollectablesData> Collectables;
        public UnityObjectRef<DialogueData> StartDialogue;
        public UnityObjectRef<DialogueData> EndDialogue;
    }

    public enum GameState
    {
        Init,
        SplashScreen,
        OpeningDialogue,
        Questing,
        ClosingDialogue,
    }

    public enum InterfaceState
    {
        Questing,
        Inventory,
        Help,
    }
}