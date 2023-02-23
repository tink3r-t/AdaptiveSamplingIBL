namespace SeeSharp.Integrators.Util;


public class AdaptiveTiler : Tiler
{

    SAT imgSat;
    TilePos[,] indexLookUp;

    List<BBox2D> leafNodes = new List<BBox2D>();

    public void findDivisions(BBox2D area, float threshold)
    {

        float total_area = imgSat.GetSum(area);
        float divisionThreshold = total_area / 2.0f;
        if (total_area < threshold || area.size.X < 4 || area.size.Y < 4)
        {
            leafNodes.Add(area);
            return;
        }

        if (area.size.X < area.size.Y)
        {

            BBox2D tiler = new BBox2D(new Vector2i(area.min.X, area.min.Y), new Vector2i(area.max.X, area.min.Y + area.size.Y / 2));
            BBox2D otiler = new BBox2D(new Vector2i(area.min.X, area.min.Y + area.size.Y / 2), new Vector2i(area.max.X, area.max.Y));
            findDivisions(tiler, threshold);
            findDivisions(otiler, threshold);
            return;

        }
        else
        {

            BBox2D tiler = new BBox2D(new Vector2i(area.min.X, area.min.Y), new Vector2i(area.min.X + area.size.X / 2, area.max.Y));
            BBox2D otiler = new BBox2D(new Vector2i(area.min.X + area.size.X / 2, area.min.Y), new Vector2i(area.max.X, area.max.Y));
            findDivisions(tiler, threshold);
            findDivisions(otiler, threshold);
            return;
        }

    }

    public AdaptiveTiler(RgbImage Image, int Gx, int Gy)
    {
        int approxTiles = Gx * Gy;

        imgSat = new SAT(Image);
        indexLookUp = new TilePos[Image.Width, Image.Height];


        BBox2D totalArea = new BBox2D(new Vector2i(0, 0), new Vector2i(Image.Width, Image.Height));
        double totalEngery = imgSat.GetSum(totalArea);

        findDivisions(totalArea, (float)(totalEngery * 2.0f / approxTiles));


        grids = new (RegularGrid2d, float)[leafNodes.Count];
        Debug.WriteLine("Girds : " + leafNodes.Count);
        for (int i = 0; i < leafNodes.Count; i++)
        {
            Debug.Assert(leafNodes[i].size.X != 0);
            Debug.Assert(leafNodes[i].size.Y != 0);
            grids[i].Item1 = new RegularGrid2d(leafNodes[i].size.X, leafNodes[i].size.Y);
            grids[i].Item2 = 0;
            for (int row = leafNodes[i].min.X; row < leafNodes[i].max.X; row++)
            {
                for (int col = leafNodes[i].min.Y; col < leafNodes[i].max.Y; col++)
                {
                    indexLookUp[row, col].tileNo = i;
                    indexLookUp[row, col].offset.X = (row - leafNodes[i].min.X) / (float)leafNodes[i].size.X;
                    indexLookUp[row, col].offset.Y = (col - leafNodes[i].min.Y) / (float)leafNodes[i].size.Y;
                    float val = Image.GetPixel(row, col).Luminance;
                    grids[i].Item1.Splat((row - leafNodes[i].min.X) / leafNodes[i].size.X, (col - leafNodes[i].min.Y) / leafNodes[i].size.Y, val);
                    grids[i].Item2 += val;
                }
            }
            grids[i].Item1.Normalize();
        }
    }


    public override float GetPointProbabilty(TilePos tilePos)
    {
        return grids[tilePos.tileNo].Item1.Pdf(new Vector2(tilePos.offset.X, tilePos.offset.Y)) / (leafNodes[tilePos.tileNo].size.X * leafNodes[tilePos.tileNo].size.Y)  * (imgSat.dimX * imgSat.dimY) / GetTilesCount(); // (float) max_value / ((float) average );
    }

    public override TilePos worldPixelToLocalPixel(Vector2 pixelPos)
    {
        int x = Math.Min((int)(pixelPos.X * (imgSat.dimX)), imgSat.dimX - 1);
        int y = Math.Min((int)(pixelPos.Y * (imgSat.dimY)), imgSat.dimY - 1);
        return new TilePos(indexLookUp[x, y].tileNo, indexLookUp[x, y].offset);

    }

    public Vector2 ConvertTilePosToPixelPos(TilePos tilePos)
    {
        return new Vector2((leafNodes[tilePos.tileNo].min.X + leafNodes[tilePos.tileNo].size.X * tilePos.offset.X) / imgSat.dimX, (leafNodes[tilePos.tileNo].min.Y + leafNodes[tilePos.tileNo].size.Y * tilePos.offset.Y) / imgSat.dimY);
    }


    public override TileSample SampleInTile(int x, Vector2 primary)
    {
        var localTilePixel = grids[x].Item1.Sample(primary);
        var pdfLocalPix = grids[x].Item1.Pdf(localTilePixel)  / (leafNodes[x].size.X * leafNodes[x].size.Y)  * (imgSat.dimY * imgSat.dimX) / GetTilesCount();

        var pixelPrimary = new Vector2((leafNodes[x].min.X + leafNodes[x].size.X * localTilePixel.X) / imgSat.dimX, (leafNodes[x].min.Y + leafNodes[x].size.Y * localTilePixel.Y) / imgSat.dimY);
        
        Debug.Assert(pixelPrimary.X >= 0 && pixelPrimary.Y <= 1);
        Debug.Assert(pixelPrimary.Y >= 0 && pixelPrimary.Y <= 1);

        TilePos tp = new TilePos { tileNo = x, offset = localTilePixel };

        return new TileSample
        {
            worldpixel = ConvertTilePosToPixelPos(tp),
            PdfInTile = pdfLocalPix,
        };
    }

    public override int GetTileIndexByWorldPos(Vector2 worldPos)
    {
        int x = Math.Min((int)(worldPos.X * (imgSat.dimX)), imgSat.dimX - 1);
        int y = Math.Min((int)(worldPos.Y * (imgSat.dimY)), imgSat.dimY - 1);
        return (indexLookUp[x, y].tileNo);
    }

    public override Vector2 GetTileStartPosition(int k)
    {
        TilePos tp = new TilePos { tileNo = k, offset = new Vector2(0, 0) };
        return ConvertTilePosToPixelPos(tp);

    }

}