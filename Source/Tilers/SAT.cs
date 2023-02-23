namespace SeeSharp.Integrators.Util;


public struct Vector2i {

    public Vector2i(int _X, int _Y) {
        X = _X;
        Y = _Y;
    }

    public int X;
    public int Y;


    public static Vector2i operator -(Vector2i a, Vector2i b) {
        return new Vector2i(a.X - b.X, a.Y - b.Y);
    }
}
public struct BBox2D {

    public BBox2D(Vector2i _min, Vector2i _max) {
        min = _min;
        max = _max;
        size = _max - _min;
        Debug.Assert(size.X > 0 && size.Y > 0);
    }

    public Vector2i size;

    public Vector2i min;
    public Vector2i max;
}

public class SAT {


    public float total_sum = 0;
    public double[,] data;

    public int dimX, dimY;
    public SAT(RgbImage image) {
        data = new double[image.Width, image.Height];
        dimX = image.Width;
        dimY = image.Height;
        data[0, 0] = image.GetPixel(0, 0).Luminance;

        for (int i = 1; i < image.Width; i++)
            data[i, 0] = image.GetPixel(i, 0).Luminance + data[i - 1, 0];

        for (int i = 1; i < image.Height; i++)
            data[0, i] = image.GetPixel(0, i).Luminance + data[0, i - 1];

        for (int i = 1; i < image.Width; i++) {
            for (int j = 1; j < image.Height; j++) {
                data[i, j] = image.GetPixel(i, j).Luminance + data[i, j - 1] + data[i - 1, j] - data[i - 1, j - 1];
            }
        }
    }

    public double GetAt(int x, int y) {
        return data[x, y];
    }


    public double GetAtSafe(int x, int y) {
        if (x < 0)
            return 0;
        if (y < 0)
            return 0;
        if (x > dimX-1)
             x = x-1;
        if (y > dimY-1)
             y = y - 1;
        return data[x, y];
    }

    public float GetSum(BBox2D area) {
        Debug.Assert((float)(GetAt(area.max.X - 1, area.max.Y - 1) - GetAt(area.max.X - 1, area.min.Y) - GetAt(area.min.X, area.max.Y - 1) + GetAt(area.min.X, area.min.Y)) >= 0);
        return (float)(GetAt(area.max.X - 1, area.max.Y - 1) - GetAt(area.max.X - 1, area.min.Y) - GetAt(area.min.X, area.max.Y - 1) + GetAt(area.min.X, area.min.Y));
    }
}