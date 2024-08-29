#ifndef _CUSTOM_FORWARD_PLUS_INCLUDE
#define _CUSTOM_FORWARD_PLUS_INCLUDE

//xy : Screen UV to tile coordinates
//z : tile per row, as integer,
//w : tile data size, as integer
float4 _ForwardPlusTileSettings;

StructuredBuffer<int> _ForwardPlusTiles;

struct ForwardPlueTile
{
    int2 coordinates;
    int index;

    int GetTileDataSize()
    {
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
};

ForwardPlueTile GetForwardPlusTile(float2 screenUV)
{
    ForwardPlueTile tile;
    tile.coordinates = int2(screenUV * _ForwardPlusTileSettings.xy);
    tile.index = tile.coordinates.y * asint(_ForwardPlusTileSettings.z) + tile.coordinates.x;
    return tile;
}

#endif
