using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

//using float4 = Unity.Mathematics.float4;

//For 作业允许对原生容器的每个元素执行相同的独立操作，或者迭代固定的次数。 此作业类型为控制作业的调度提供了最灵活的方式。
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct ForwardPlusTilesJob : IJobFor
{
    [ReadOnly]
    public NativeArray<float4> lightBounds;

    [WriteOnly, NativeDisableParallelForRestriction]
    public NativeArray<int> tileData;

    public int otherLightCount;
    public float2 tileScreenUVSize;
    public int maxLightsPerTile;
    public int tilePerRow;
    public int tileDataSize;

    //Execute(int index) 会对每个索引（从 0 到提供的长度）执行一次。
    public void Execute(int tileIndex)
    {
        //数据行列从0开始 tileIndex有从0开始 509结束 总长510
        int y = tileIndex / tilePerRow;
        int x = tileIndex - y * tilePerRow;
        var tileBounds = float4(x, y, x + 1, y + 1) * tileScreenUVSize.xyxy;

        //计算当前tile中第0个数据在tileData中的索引
        int headerIndex = tileIndex * tileDataSize;

        int dataIndex = headerIndex;
        int lightsInTileCount = 0;

        for (int i = 0; i < otherLightCount; i++)
        {
            float4 lightBound = lightBounds[i];
            //判断light的包围盒在不在给定的单个包围盒中
            if (all(float4(lightBound.xy, tileBounds.xy) <= float4(tileBounds.zw, lightBound.zw)))
            {
                dataIndex++;
                tileData[dataIndex] = i;
                lightsInTileCount++;
                if (lightsInTileCount >= maxLightsPerTile)
                {
                    break;
                }
            }
        }

        tileData[headerIndex] = lightsInTileCount;
    }
}