using Unity.Collections;

namespace Unity.Physics.Stateful
{
    public struct StatefulSimulationEventBuffers<T> where T : unmanaged, IStatefulSimulationEvent<T>
    {
        public NativeList<T> Previous;
        public NativeList<T> Current;

        public void AllocateBuffers()
        {
            Previous = new NativeList<T>(Allocator.Persistent);
            Current = new NativeList<T>(Allocator.Persistent);
        }

        public void Dispose()
        {
            if (Previous.IsCreated) Previous.Dispose();
            if (Current.IsCreated) Current.Dispose();
        }

        public void SwapBuffers()
        {
            var tmp = Previous;
            Previous = Current;
            Current = tmp;
            Current.Clear();
        }

        /// <summary>
        /// 返回基于上一个和当前事件框架的有状态事件的排序组合列表。
        /// Note: 首先调用 SortBuffers 确保帧缓冲区已排序。
        /// </summary>
        /// <param name="statefulEvents"></param>
        /// <param name="sortCurrent">Specifies 当前事件列表是否需要排序 first.</param>
        public void GetStatefulEvents(NativeList<T> statefulEvents, bool sortCurrent = true) => GetStatefulEvents(Previous, Current, statefulEvents, sortCurrent);

        /// <summary>
        /// 给定两个排序的事件缓冲区，此函数返回一个组合列表
        /// 每个事件上设置的所有适当的 <see cref="StatefulEventState"/>。
        /// </summary>
        /// <param name="previousEvents">The 前一帧的事件缓冲区。该列表应该已经从之前的 frame.</param> 排序
        /// <param name="currentEvents">The 当前帧的事件缓冲区。在调用此 function.</param> 之前应对该列表进行排序
        /// <param name="statefulEvents">A 基于先前和当前 frames.</param> 的状态事件的单个组合列表
        /// <param name="sortCurrent">Specifies currentEvents 列表是否需要排序 first.</param>
        public static void GetStatefulEvents(NativeList<T> previousEvents, NativeList<T> currentEvents, NativeList<T> statefulEvents, bool sortCurrent = true)
        {
            if (sortCurrent) currentEvents.Sort();

            statefulEvents.Clear();

            int c = 0;
            int p = 0;
            while (c < currentEvents.Length && p < previousEvents.Length)
            {
                int r = previousEvents[p].CompareTo(currentEvents[c]);
                if (r == 0)
                {
                    var currentEvent = currentEvents[c];
                    currentEvent.State = StatefulEventState.Stay;
                    statefulEvents.Add(currentEvent);
                    c++;
                    p++;
                }
                else if (r < 0)
                {
                    var previousEvent = previousEvents[p];
                    previousEvent.State = StatefulEventState.Exit;
                    statefulEvents.Add(previousEvent);
                    p++;
                }
                else //(r > 0)
                {
                    var currentEvent = currentEvents[c];
                    currentEvent.State = StatefulEventState.Enter;
                    statefulEvents.Add(currentEvent);
                    c++;
                }
            }
            if (c == currentEvents.Length)
            {
                while (p < previousEvents.Length)
                {
                    var previousEvent = previousEvents[p];
                    previousEvent.State = StatefulEventState.Exit;
                    statefulEvents.Add(previousEvent);
                    p++;
                }
            }
            else if (p == previousEvents.Length)
            {
                while (c < currentEvents.Length)
                {
                    var currentEvent = currentEvents[c];
                    currentEvent.State = StatefulEventState.Enter;
                    statefulEvents.Add(currentEvent);
                    c++;
                }
            }
        }
    }
}
