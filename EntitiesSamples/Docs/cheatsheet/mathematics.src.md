# Unity.Mathematics 备忘单

*数学中的大多数方法对于不同类型的组合都有许多重载。例如，`math.abs()` 采用向量参数，而不仅仅是标量，e.g。`math.abs(new int3(5, -7, -1))` 返回 `new int3(5, 7, 1)`。此备忘单并未详尽地演示所有重载。有关完整列表，请参阅 [API 参考](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/)。*

在此页面中：

* [类型](#types)
* [矢量创建和复制](#vector-creation-and-copying)
* [矩阵创建与复制](#matrix-creation-and-copying)
* [向量和矩阵运算符](#vector-and-matrix-operators)
* [算术](#arithmetic)
* [指数和对数](#exponents-and-logarithms)
* [四舍五入和符号](#rounding-and-signs)
* [值检查](#value-checks)
* [转换](#conversion)
* 【Interpolation 及夹紧】(#interpolation-and-clamping)
* [在两个值之间选择](#picking-between-two-values)
* [哈希](#hashing)
* [按位和布尔运算](#bitwise-and-boolean-operations)
* [三角函数、角度和弧度](#trig-degrees-and-radians)
* [矢量几何](#vector-geometry)
* [基本方向向量](#cardinal-direction-vectors)
* [旋转和变换](#rotations-and-transforms)
* [生成随机数](#generating-random-numbers)
* [产生噪音](#generating-noise)

<br>

## 类型

|||
| ----- | ----------- |
|[`math`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.math.html)| 包含许多静态数学常量和方法的类。|
|[`noise`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.html)|包含用于生成噪声的静态方法的类。|
|[`quaternion`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.quaternion.html)|表示旋转的结构。|
|[`Random`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.Random.html)|用于生成随机数的结构。|
|[`RigidTransform`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.RigidTransform.html)| 表示变换矩阵的结构。|

<br>

### 标量类型：

|||
| ----- | ----------- |
| [`half`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.half.html) | 16 位浮点数。 |

<br>

### 矢量类型：

||
| ----- |
|[`bool2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool2.html)、[`bool3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool3.html)、[`bool4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool4.html) |
|[`int2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int2.html)、[`int3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int3.html)、[`int4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int4.html) |
|[`uint2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint2.html)、[`uint3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint3.html)、[`uint4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint4.html) |
|[`float2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float2.html)、[`float3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float3.html)、[`float4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float4.html) |
|[`half2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.half2.html)、[`half3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.half3.html)、[`half4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.half4.html) |
|[`double2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double2.html)、[`double3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double3.html)、[`double4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double4.html) |

<br>

### 矩阵类型：

||
| ---- |
|[`bool2x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool2x2.html)、[`bool2x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool2x3.html)、[`bool2x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool2x4.html)、[`bool3x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool3x2.html)、[`bool3x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool3x3.html)、[`bool3x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool3x4.html)、[`bool4x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool4x2.html)、[`bool4x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool4x3.html)、[`bool4x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.bool4x4.html) |
|[`int2x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int2x2.html)、[`int2x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int2x3.html)、[`int2x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int2x4.html)、[`int3x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int3x2.html)、[`int3x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int3x3.html)、[`int3x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int3x4.html)、[`int4x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int4x2.html)、[`int4x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int4x3.html)、[`int4x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.int4x4.html) |
|[`uint2x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint2x2.html)、[`uint2x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint2x3.html)、[`uint2x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint2x4.html)、[`uint3x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint3x2.html)、[`uint3x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint3x3.html)、[`uint3x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint3x4.html)、[`uint4x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint4x2.html)、[`uint4x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint4x3.html)、[`uint4x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.uint4x4.html) |
|[`float2x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float2x2.html)、[`float2x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float2x3.html)、[`float2x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float2x4.html)、[`float3x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float3x2.html)、[`float3x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float3x3.html)、[`float3x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float3x4.html)、[`float4x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float4x2.html)、[`float4x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float4x3.html)、[`float4x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.float4x4.html) |
|[`double2x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double2x2.html)、[`double2x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double2x3.html)、[`double2x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double2x4.html)、[`double3x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double3x2.html)、[`double3x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double3x3.html)、[`double3x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double3x4.html)、[`double4x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double4x2.html)、[`double4x3`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double4x3.html)、[`double4x4`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.double4x4.html) |

<br>

## 矢量创建和复制

[!code-no-using](../Projects/MarkdownSrc/Assets/Examples/Mathematics.cs#vector_creation)

<br>

## 矩阵创建和复制

[!code-no-using](../Projects/MarkdownSrc/Assets/Examples/Mathematics.cs#matrix_creation)

<br>

## 向量和矩阵运算符

*向量和矩阵运算符（`+`、`-`、`*`、`/`、`%`、`--`、`++`、`==`、`!=`、`<`、`>`、`<=`、`>=`）对相应的 component 对进行操作：*

[!code-no-using](../Projects/MarkdownSrc/Assets/Examples/Mathematics.cs#vector_matrix_operators)

*整数向量和矩阵类型也有按位运算符：`&`、`|`、`~`、`<<`、`>>`。*

<br>

## 算术

|||
| ----- | ----------- |
|[`math.fmod`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.fmod.html)| x/y 的浮点余数。|
|[`math.mad`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.mad.html)| 三个标量或向量上的分量 (a * b + c)。 |
|[`math.modf`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.modf.html)| 模数和分数 component。|
|[`math.csum`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.csum.html)| 向量 components 的水平总和。|
|[`math.rcp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.rcp.html)| 倒数（1 除以值）。|

<br>

## 指数和对数

|||
| ----- | ----------- |
|[`math.ceillog2`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.ceillog2.html)| 以 2 为底的对数的上限。 |
|[`math.ceilpow2`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.ceilpow2.html)| 大于或等于输入的两个的最小幂。|
|[`math.exp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.exp.html)| 常数 e 的幂。 |
|[`math.exp10`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.exp10.html)| 值 10 的幂。 |
|[`math.exp2`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.exp2.html)| 值 2 的幂。 |
|[`math.floorlog2`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.floorlog2.html)| 以 2 为底的对数的下限。 |
|[`math.log`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.log.html)| 自然对数。 |
|[`math.log10`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.log10.html)| 以 10 为底的对数 |
|[`math.log2`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.log2.html)| 以 2 为底的对数。 |
|[`math.pow`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.pow.html)| 提升为权力。 |
|[`math.rsqrt`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.rsqrt.html)| 平方根的倒数。 |
|[`math.sqrt`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.sqrt.html)| 平方根。 |

<br>

## 舍入和符号

|||
| ----- | ----------- |
|[`math.abs`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.abs.html)| 绝对值。 |
|[`math.ceil`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.ceil.html)| 围捕。 |
|[`math.floor`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.floor.html)| 向下舍入。 |
|[`math.round`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.round.html)| 四舍五入到最接近的值。 |
|[`math.sign`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.sign.html)| 值的符号：+1、0 或 -1 |

<br>

## 价值检查

|||
| ----- | ----------- |
|[`math.isfinite`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.isfinite.html)| 是有限浮点值吗？ |
|[`math.isinf`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.isinf.html)| 浮点值是无穷大吗？ |
|[`math.isnan`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.isnan.html)| 是 NaN 吗？ |
|[`math.ispow2`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.ispow2.html)| 是 2 的幂吗？ |

<br>

## 转换

|||
| ----- | ----------- |
|[`math.asdouble`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.asdouble.html)| 将 64 位整数的位重新解释为双精度数。 |
|[`math.asfloat`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.asfloat.html)| 将 32 位整数的位重新解释为浮点数。 |
|[`math.asint`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.asint.html)| 将 float 或 uint 的位重新解释为 int。 |
|[`math.aslong`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.aslong.html)| 将双精度或 64 位整数的位重新解释为 long。 |
|[`math.asulong`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.asulong.html)| 将双精度或 64 位整数的位重新解释为 ulong。  |
|[`math.asuint`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.asuint.html)| 将 float 或 uint 的位重新解释为 uint。 |
|[`math.f16tof32`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.f16tof32.html)| 半精度浮点值的浮点表示形式。 |
|[`math.f32tof16`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.f32tof16.html)| 浮点值的最接近的半精度浮点表示形式。 |
|[`math.frac`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.frac.html)| 浮点值的小数部分。 |
|[`math.trunc`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.trunc.html)| 浮点值的整数部分（四舍五入为零）。 |

<br>

## Interpolation 及夹紧

|||
| ----- | ----------- |
|[`math.clamp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.clamp.html)| 将一个值限制在一个区间内。 |
|[`math.lerp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.lerp.html)| 两个值之间的线性 interpolation。 |
|[`math.nlerp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.nlerp.html)| 两个四元数之间的归一化线性 interpolation。 |
|[`math.remap`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.remap.html)| 将值从源范围线性重新映射到目标范围。|
|[`math.saturate`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.saturate.html)| 将一个值限制在区间 [0, 1] 内。 |
|[`math.slerp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.slerp.html)| 两个四元数之间的球形 interpolation。 |
|[`math.smoothstep`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.smoothstep.html)| 在 0.0f 和 1.0f 之间平滑 Hermite interpolation。 |
|[`math.step`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.step.html)| 如果 x >= y，则返回 1.0f，否则返回 0.0f。 |
|[`math.unlerp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.unlerp.html)| 将值标准化为范围。（与 lerp 相反。） |

<br>

## 在两个值之间进行选择

|||
| ----- | ----------- |
|[`math.shuffle`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.shuffle.html)| 从两个向量中挑选一个或多个特定的 components。 |
|[`math.select`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.select.html)| 根据布尔值在两个值之间进行选择。与三元运算符类似，但您也可以选择两个向量的特定 components。 |
|[`math.cmax`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.cmax.html)| 向量的最大 component。 |
|[`math.cmin`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.cmin.html)| 向量的最小 component。 |
|[`math.max`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.max.html)| 两个值中最大的一个。 |
|[`math.min`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.min.html)| 两个值中最小的一个。 |

<br>

## 散列

*有关更多哈希选项，请参阅集合 package。*

|||
| ----- | ----------- |
|[`math.hash`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.hash.html)| 值的哈希值。 |
|[`math.hashwide`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.hashwide.html)| 当对多个值进行哈希处理时，将它们分别传递给 hashwide()，组合结果，然后 hash() 组合通常会更有效。 |

<br>

## 按位和布尔运算

|||
| ----- | ----------- |
|[`math.all`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.all.html)| 如果所有布尔值都为真，则为真。|
|[`math.any`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.any.html)| 如果任何布尔值为真，则为真。|
|[`math.bitmask`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.bitmask.html)| bool4 的位掩码：每个 component 一位（总共 4 位），按 LSB 顺序（从低到高）。 |
|[`math.countbits`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.countbits.html)| 1 位的计数。|
|[`math.compress`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.compress.html)|  将向量的启用掩码的 components 打包到左侧。|
|[`math.lzcnt`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.lzcnt.html)|  位的前导零计数。|
|[`math.reversebits`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.reversebits.html)|  反转位的顺序。|
|[`math.rol`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.rol.html)|  向左循环位。|
|[`math.ror`](ZXQXOPWDQTW​​CICXZXQ)|  向右循环位。|
|[`math.tzcnt`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.tzcnt.html)| 位的尾随零计数。|

<br>

## 三角函数、角度和弧度

|||
| ----- | ----------- |
|[`math.acos`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.acos.html)|反余弦。|
|[`math.asin`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.asin.html)|反正弦。|
|[`math.atan`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.atan.html)|Arctangent
|[`math.atan2`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.atan2.html)|2 个参数的反正切。 |
|[`math.cos`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.cos.html)|余弦。|
|[`math.cosh`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.cosh.html)|双曲余弦。|
|[`math.math.degrees`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.degrees.html)| 度数以弧度表示。|
|[`math.radians`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.radians.html)| 弧度来自度数。|
|[`math.sin`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.sin.html)|正弦。|
|[`math.sincos`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.sincos.html)|正弦。|
|[`math.sinh`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.sinh.html)|双曲正弦。|
|[`math.tan`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.tan.html)|切线。|
|[`math.tanh`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.tanh.html)|双曲正切。|

<br>

## 矢量几何

|||
| ----- | ----------- |
|[`math.cross`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.cross.html)| 叉积。 |
|[`math.distance`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.distance.html)| 两点之间的距离（1 到 4 维）。 |
|[`math.distancesq`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.distancesq.html)| 两点之间距离的平方根（1 到 4 维）。 |
|[`math.dot`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.dot.html)| 点积。 |
|[`math.faceforward`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.faceforward.html)| 如果另外两个向量指向同一方向，则翻转该向量（i.e。它们之间的角度小于或等于 90 度）。 |
|[`math.length`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.length.html)| 点到原点的距离。 |
|[`math.lengthsq`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.lengthsq.html)| 点距原点距离的平方根。 |
|[`math.normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.normalize.html)| 归一化向量。 |
|[`math.normalizesafe`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.normalizesafe.html)| 归一化向量。如果归一化向量不是有限的，则返回默认值。 |
|[`math.project`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.project.html)| 将一个向量投影到另一个向量上。 |
|[`math.projectsafe`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.projectsafe.html)| 将一个向量投影到另一个向量上。如果投影向量不是有限的，则返回默认值。 |
|[`math.reflect`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.reflect.html)| 入射矢量和法向矢量的反射。 |
|[`math.refract`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.refract.html)| 入射矢量和法线矢量的折射。 |
|[`math.transform`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.transform.html)| Transform 具有 4x4 矩阵的 3 维向量。 |

<br>

## 基本方向向量

|||
| ----- | ----------- |
|[`math.forward`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.forward.html)| Unity 坐标中的前向轴。 |
|[`math.back`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.back.html)| Unity 坐标中的后轴。 |
|[`math.up`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.up.html)| Unity 坐标中的上轴。 |
|[`math.down`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.down.html)| Unity 坐标中的下轴。 |
|[`math.left`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.left.html)| Unity 坐标中的左轴。 |
|[`math.right`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.right.html)| Unity 坐标中的右轴。 |

<br>

## 矩阵运算

|||
| ----- | ----------- |
|[`math.determinant`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.determinant.html)| 矩阵的行列式。  |
|[`math.fastinverse`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.fastinverse.html)| 刚性变换的快速矩阵逆（正交基和平移）。 |
|[`math.inverse`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.inverse.html)| 矩阵或四元数的逆。  |
|[`math.mul`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.mul.html)| 矩阵乘法。|
|[`math.transpose`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.transpose.html)| 矩阵的转置。  |
|[`math.unitlog`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.unitlog.html)| 单位长度四元数的自然对数。 |

<br>

## 旋转和变换

|||
| ----- | ----------- |
|[`math.conjugate`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.conjugate.html)| 共轭四元数。（翻转 x、y 和 z 的符号，但不翻转 w。） |
|[`math.orthonormalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.orthonormalize.html)| 对 float3x3 矩阵进行正交归一化。 |
|[`math.rotate`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.rotate.html)| 将向量旋转单位四元数。 |
|[`math.unitexp`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.unitexp.html)| 四元数的自然指数。（假设 w 为零。） |
|[`quaternion.AxisAngle`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.AxisAngle.html)| 轴角旋转的四元数表示。 |
|[`quaternion.Euler`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.Euler.html)| 欧拉角旋转的四元数表示（由参数指定的轴顺序）。 |
|[`quaternion.EulerXYZ`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.EulerXYZ.html)| 欧拉角旋转的四元数表示（轴顺序 XYZ）。 |
|[`quaternion.EulerXZY`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.EulerXZY.html)| 欧拉角旋转的四元数表示（轴顺序 XZY）。 |
|[`quaternion.EulerYXZ`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.EulerYXZ.html)| 欧拉角旋转的四元数表示（轴顺序 YXZ）。 |
|[`quaternion.EulerYZX`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.EulerYZX.html)| 欧拉角旋转的四元数表示（轴顺序 YZX）。 |
|[`quaternion.EulerZXY`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.EulerZXY.html)| 欧拉角旋转的四元数表示（轴顺序 ZXY）。 |
|[`quaternion.EulerZYX`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.EulerZYX.html)| 欧拉角旋转的四元数表示（轴顺序 ZYX）。 |
|[`quaternion.LookRotation`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.LookRotation.html)| 表示从单位长度前向向量和单位长度向上向量导出的旋转的四元数。 |
|[`quaternion.LookRotationSafe`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.LookRotationSafe.html)| 表示从前向向量和向上向量导出的旋转的四元数。 |
|[`quaternion.RotateX`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.RotateX.html)| 绕 X 轴旋转的四元数表示。 |
|[`quaternion.RotateY`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.RotateY.html)| 绕 Y 轴旋转的四元数表示。 |
|[`quaternion.RotateZ`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.quaternion.RotateZ.html)| 绕 Z 轴旋转的四元数表示。 |
|[`float4x4.LookAt`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.LookAt.html)| 从眼睛位置、目标点和单长度向上向量导出的视图矩阵。 |
|[`float4x4.Ortho`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.Ortho.html)| 正交投影矩阵 |
|[`float4x4.OrthoOffCenter`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.OrthoOffCenter.html)| 偏心正交投影矩阵。 |
|[`float4x4.PerspectiveFov`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.PerspectiveFov.html)| 基于视场的透视投影矩阵。 |
|[`float4x4.PerspectiveOffCenter`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.PerspectiveOffCenter.html)| 偏心透视投影矩阵 |
|[`float4x4.Scale`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.Scale.html)| 表示尺度变换的矩阵。 |
|[`float4x4.Translate`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.Translate.html)| 表示平移变换的矩阵。 |
|[`float4x4.TRS`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float4x4.TRS.html)| 表示组合平移、旋转和缩放变换的矩阵。 |
|[`float3x3.Scale`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.float3x3.Scale.html)| 表示尺度变换的矩阵。 |
|[`RigidTransform.Translate`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.RigidTransform.Translate.html)| 表示平移变换的矩阵。 |

*`RigidTransform`、`float3x3` 和 `float4x4` 也具有与 `quaternion` 大部分相同的旋转方法。*

<br>

## 生成随机数

[！代码不使用](../Projects/MarkdownSrc/Assets/Examples/Mathematics.cs#random)

<br>

## 产生噪音

|||
| ----- | ----------- |
| [`noise.cellular(float2)`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.cellular.html) | 2D 蜂窝噪声（“Worley 噪声”），具有标准 3x3 搜索窗口，可获取良好的特征点值。 |
| [`noise.cellular(float3)`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.cellular.html) | 3D 蜂窝噪声（“Worley 噪声”），具有 3x3x3 搜索区域，到处都有良好的 F2，但比 2x2x2 版本慢很多。 |
| [`noise.cellular2x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.cellular2x2.html) | 具有 2x2 搜索窗口的 2D 蜂窝噪声（“Worley 噪声”）。  |
| [`noise.cellular2x2x2`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.cellular2x2x2.html) | 具有 2x2x2 搜索窗口的 3D 蜂窝噪声（“Worley 噪声”）。 |
| [`noise.cnoise`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.cnoise.html) | 经典的柏林噪音。 |
| [`noise.pnoise`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.pnoise.html) | 经典柏林噪声，周期性变体。  |
| [`noise.psrdoise`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.psrdoise.html) | 具有固定或旋转梯度和解析导数的二维平铺单纯形噪声。 |
| [`noise.psrnoise`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.psrnoise.html) | 具有固定或旋转梯度的二维平铺单纯形噪声，但没有解析导数。  |
| [`noise.snoise`](ZXQCianSCPEMJTFZXQ) | 单纯形噪声。 |
| [`noise.srdnoise`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.srdnoise.html) | 具有固定或旋转梯度和解析导数的二维非平铺单纯形噪声。 |
| [`noise.srnoise`](https://docs.unity3d.com/Packages/com.unity.mathematics@latest/index.html?subfolder=/api/Unity.Mathematics.noise.srnoise.html) | 具有固定或旋转梯度的二维非平铺单纯形噪声，没有解析导数。 |
