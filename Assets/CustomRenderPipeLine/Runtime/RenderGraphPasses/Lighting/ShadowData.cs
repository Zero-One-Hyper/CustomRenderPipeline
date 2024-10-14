using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public partial class Shadows
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DirectionShadowCascade
    {
        public const int Stride = 4 * 4 * 2;
        public Vector4 cullingSphere;
        public Vector4 cascadeData;

        public DirectionShadowCascade(Vector4 cullingSphere, float tileSize,
            float filterSize)
        {
            //把求级联阴影倒数部分放到C#来做
            //_cascadeData[index].x = 1.0f / cullingSphere.w;
            //只考虑单个维度的话，沿表面法相偏移进行阴影采样 只需偏移世界空间下纹素大小
            float texelSize = 2.0f * cullingSphere.w / tileSize;
            //float filterSize = texelSize * ((float)filterMode + 1.0f); //使用PCF的过滤等级自动调整偏移
            filterSize *= texelSize;
            //先在C#中计算平方 （本来需要在在着色器中计算表面与球心距离的平方及半径的平方）
            cullingSphere.w -= texelSize;
            cullingSphere.w *= cullingSphere.w;
            this.cullingSphere = cullingSphere;
            cascadeData = new Vector4(
                1.0f / cullingSphere.w,
                filterSize * 1.4142136f); //纹素时正方形，最坏情况下需要考虑斜边（需要最大）
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OtherShadowData
    {
        public const int Stride = 4 * 4 + 4 * 16;
        public Vector4 tileData;
        public Matrix4x4 shadowMatrix;

        public OtherShadowData(Vector2 offset, float scale, float bias, float border, Matrix4x4 shadowMatrix)
        {
            //保证钳位采样
            tileData.x = offset.x * scale + border;
            tileData.y = offset.y * scale + border;
            tileData.z = scale - border - border;
            tileData.w = bias;
            this.shadowMatrix = shadowMatrix;
        }
    }
}