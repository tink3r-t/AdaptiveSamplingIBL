namespace SeeSharp.Integrators.Util;
public struct TileSample {
    public Vector2 worldpixel;
    public float PdfInTile;
}

public struct TilePos{

    public TilePos(){
        tileNo = -1;
        offset = new Vector2(0,0);
    }

    
    public TilePos(int tileN, Vector2 off){
        tileNo = tileN;
        offset = off;
    }


    public int tileNo;
    public Vector2 offset;
}

public abstract class Tiler {

    protected (RegularGrid2d,float)[] grids;

    public abstract float GetPointProbabilty(TilePos tilePos);
    //public abstract float GetTileMagnitude(int tileNo);
    public abstract TilePos worldPixelToLocalPixel(Vector2 position);
    public abstract int GetTileIndexByWorldPos(Vector2 worldPos);
    public abstract TileSample SampleInTile(int tileNo, Vector2 sampleAt);

    public float GetTileMagnitude(int tile) {
        return grids[tile].Item2;
    }
    public abstract Vector2 GetTileStartPosition(int k);

    public int GetTilesCount(){
        return grids.Length;
    }

}