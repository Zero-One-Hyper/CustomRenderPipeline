#ifndef _CUSTOM_FORWARD_PLUS_INCLUDE
#define _CUSTOM_FORWARD_PLUS_INCLUDE

//xy : Screen UV to tile coordinates
//z : tile per row, as integer,
//w : tile data size, as integer
float4 _ForwardPlusTileSettings;

StructuredBuffer<int> _ForwardPlusTiles;

struct ForwardPlusTile
{
    int2 coordinates;
    int index;

    int GetTileDataSize()
    {
        //return (_ForwardPlusTileSettings.w);
        return asint(_ForwardPlusTileSettings.w);
    }

    int GetHeaderIndex()
    {
        return index * GetTileDataSize();
    }

    int GetLightCount()
    {
        return _ForwardPlusTiles[GetHeaderIndex()];
    }

    int GetFirstLightIndexInTile()
    {
        //Tile针对其他光源 headerIndex中的数据总是为0(或者说为平行光，当然这里还没添加进去)
        return GetHeaderIndex() + 1;
    }

    int GetLastLightIndexInTile()
    {
        return GetHeaderIndex() + GetLightCount();
    }

    int GetLightIndex(int lightIndexInTile)
    {
        return _ForwardPlusTiles[lightIndexInTile];
    }

    bool IsMinimumEdgePixel(float2 screenUV)
    {
        float2 startUV = coordinates / _ForwardPlusTileSettings.xy;
        return any(screenUV - startUV < _CameraBufferSize.xy);
    }

    int GetMaxLightPerTile()
    {
        return GetTileDataSize() - 1;
    }

    int2 GetScreenSize()
    {
        return int2(round(_CameraBufferSize.zw / _ForwardPlusTileSettings.xy));
    }
};

ForwardPlusTile GetForwardPlusTile(float2 screenUV)
{
    ForwardPlusTile tile;
    tile.coordinates = int2(screenUV * _ForwardPlusTileSettings.xy);
    //asint将位模式解释为整数
    tile.index = tile.coordinates.y * asint(_ForwardPlusTileSettings.z) + tile.coordinates.x;
    //tile.index = tile.coordinates.y * (_ForwardPlusTileSettings.z) + tile.coordinates.x;


    return tile;
}

#endif
