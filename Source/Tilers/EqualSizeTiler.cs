namespace SeeSharp.Integrators.Util;

public class EqualSizeTiler : Tiler
{
    int En_X;
    int En_Y;

    public EqualSizeTiler(RgbImage Image, int Gx, int Gy)
    {
        En_X = Gx;
        En_Y = Gy;


        float dx = Image.Width / (float)(Gx);
        float dy = Image.Height / (float)(Gy);

        Debug.Assert(dy == (int)dy, "Environment Map Y axis not divisible into integer parts");
        Debug.Assert(dx == (int)dx, "Environment Map X axis not divisible into integer parts");

        grids = new (RegularGrid2d, float)[Gx * Gy];

        for (int x = 0; x < Gx; x++)
        {
            for (int y = 0; y < Gy; y++)
            {
                grids[y * En_X + x].Item1 = new RegularGrid2d((int)(dx), (int)(dy));
                for (int row = (int)(x * dx); row < (x + 1) * dx; row++)
                {
                    for (int col = (int)(y * dy); col < (y + 1) * dy; col++)
                    {
                        float val = Image.GetPixel(row, col).Luminance;
                        var px = (row - (x * dx)) / dx;
                        var py = (col - (y * dy)) / dy;
                        grids[y * En_X + x].Item1.Splat(px, py, val);
                        grids[y * En_X + x].Item2 += val;
                    }
                }
                grids[y * En_X + x].Item1.Normalize();
            }
        }
    }

    public override float GetPointProbabilty(TilePos tilePos)
    {
        return grids[tilePos.tileNo].Item1.Pdf(tilePos.offset);
    }

    public override TilePos worldPixelToLocalPixel(Vector2 pixelPos)
    {
        float x = (pixelPos.X * (En_X));
        float y = (pixelPos.Y * (En_Y));
        return new TilePos(Math.Min((int)y, En_Y - 1) * En_X + Math.Min((int)x, En_X - 1),
        new Vector2(x - (int)x, y - (int)y));
    }

    public override int GetTileIndexByWorldPos(Vector2 worldPos)
    {
        float x = (worldPos.X * (En_X));
        float y = (worldPos.Y * (En_Y));

        return Math.Min((int)y, En_Y - 1) * En_X + Math.Min((int)x, En_X - 1);
    }

    public override TileSample SampleInTile(int tileNo, Vector2 primary)
    {
        var localTilePixel = grids[tileNo].Item1.Sample(primary);
        var pdfLocalPix = grids[tileNo].Item1.Pdf(localTilePixel);

        int y = (int)(tileNo / (float)En_X);
        int x = (int)(tileNo % En_X);
        var pixelPrimary = new Vector2((x + localTilePixel.X) / En_X, (y + localTilePixel.Y) / En_Y);

        Debug.Assert(pdfLocalPix > 0);

        return new TileSample
        {
            worldpixel = pixelPrimary,
            PdfInTile = pdfLocalPix,
        };
    }

    public override Vector2 GetTileStartPosition(int tileNo)
    {
        int y = (int)(tileNo / (float)En_X);
        int x = (int)(tileNo % En_X);
        return new Vector2((x) / En_X, (y) / En_Y);

    }

}