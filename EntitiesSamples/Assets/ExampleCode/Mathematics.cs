using Unity.Collections;
using Unity.Mathematics;

namespace ExampleCode.Math
{
    public class Mathematics
    {
        public static void vectorCreation()
        {
            int4 i4 = new int4(1, 2, 3, 4); // x、y、z、w
            int2 i2 = int2.zero; // new int2(0, 0);

            // 像数组一样索引 components。
            int i = i4[2]; // int i = i4.z
            i4[0] = 9; // i4.x = 9

            // 通过复制组合创建向量
            // 来自另一个向量的值（swizzling）。
            i4 = i4.xwyy; // new int4(i4.x, i4.w, i4.y, i4.y);
            i2 = i4.yz; // new int2(i4.y, i4.z);
            i4 = i2.yxyx; // new int4(i2.y, i2.x, i2.y, i2.x);

            // 从组合创建向量
            // 低维向量和标量。
            i4 = new int4(1, i2, 3); // new int4(1, i2.x, i2.y, 3);
            i4 = new int4(i2, i2); // new int4(i2.x, i2.y, i2.x, i2.y);
            i4 = new int4(7); // new int4(7, 7, 7, 7);
            i2 = new int2(7.5f); // new int2((int) 7.5f, (int) 7.5f);

            // 通过转换创建向量。
            i4 = (int4)7; // new int4(7, 7, 7, 7);
            i2 = (int2)7.5f; // new int2((int) 7.5f, (int) 7.5f);
        }

        public static void matrixCreation()
        {
            // 值按行主序排列。
            int2x3 m = new int2x3(1, 2, 3, 4, 5, 6); // 第一行：1、2、3
            // 第二行：4、5、6

            // 第一列：new int2(1, 4)
            int2 i2 = m.c0;

            // 第三列：new int2(3, 6)
            i2 = m.c2;

            // new int2x3(100, 100, 100, 100, 100, 100)
            m = new int2x3(100);

            m = new int2x3(
                new int2(1, 2), // 第 0 列
                new int2(3, 4), // 第 1 栏
                new int2(5, 6)); // 第 2 栏

            // 将每个 int component 转换为浮点数。
            float2x3 m2 = new float2x3(m);
        }

        public static void vectorMatrixOperators()
        {
            int2 a = new int2(1, 2);
            int2 b = new int2(3, 4);

            // 添加。
            int2 c = a + b; // new int2(a.x + b.x, a.y + b.y)

            // 否定。
            c = -a; // new int2(-a.x, -a.y)

            // 平等。
            bool myBool = a.Equals(b); // a.x == b.x && a.y == b.y
            bool2 myBool2 = a == b; // new int2(a.x == b.x, a.y == b.y)

            // 大于。
            myBool2 = a > b; // new bool2(a.x > b.x, a.y > b.y)
        }

        public static void random()
        {
            Random rand = new Random(123); // 123 的种子

            // [-2147483647, 2147483647]
            int integer = rand.NextInt();

            // [25, 100)
            integer = rand.NextInt(25, 100);

            // x 为 [0, 1)，y 为 [0, 1)
            float2 f2 = rand.NextFloat2();

            // x 为 [0, 7.5)，y 为 [0, 11)
            f2 = rand.NextFloat2(new float2(7.5f, 11f));

            // x 为 [2, 7.5)，y 为 [-4.6, 11)
            f2 = rand.NextFloat2(new float2(2f, -4.6f), new float2(7.5f, 11f));

            // 均匀随机的单位长度方向向量。
            double3 d3 = rand.NextDouble3Direction();

            // 均匀随机单位长度四元数。
            quaternion q = rand.NextQuaternionRotation();

            // 使用增量种子创建多个随机数生成器。
            NativeArray<Random> rngs = new NativeArray<Random>(10, Allocator.Temp);
            for (int i = 0; i < 10; i++)
            {
                // 与 Random 构造函数不同，CreateFromIndex 对种子进行哈希处理。
                // 如果我们要将递增的种子传递给构造函数，
                // 每个 RNG 都会产生类似的随机数流
                // 彼此一样。因为我们在这里使用 CreateFromIndex，
                // RNG 将各自生成随机数流
                // 完全不同且与其他无关。
                rngs[i] = Random.CreateFromIndex((uint)(i + 123));
            }
        }
    }
}
