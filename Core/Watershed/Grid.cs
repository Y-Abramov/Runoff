using System;

namespace AbrRunoff.Core.Watershed
{
    /// <summary>Точка плана. Своя, чтобы ядро не зависело от Topomatic.Cad.Foundation.</summary>
    public struct Pt
    {
        public double X;
        public double Y;
        public Pt(double x, double y) { X = x; Y = y; }
    }

    /// <summary>
    /// Регулярная сетка отметок. Построчная: index = iy * Nx + ix.
    /// NoData - NaN: отдельный массив признаков не заводим, NaN не может быть
    /// настоящей отметкой и не путается с нулём (ноль - законная отметка).
    /// </summary>
    public sealed class Grid
    {
        public readonly double OriginX;
        public readonly double OriginY;
        public readonly double Step;
        public readonly int Nx;
        public readonly int Ny;
        public readonly double[] Z;

        public Grid(double originX, double originY, double step, int nx, int ny)
        {
            if (step <= 0.0) throw new ArgumentOutOfRangeException("step");
            if (nx <= 0 || ny <= 0) throw new ArgumentOutOfRangeException("nx");

            OriginX = originX;
            OriginY = originY;
            Step = step;
            Nx = nx;
            Ny = ny;
            Z = new double[nx * ny];
            for (int i = 0; i < Z.Length; i++) Z[i] = double.NaN;
        }

        public int Count { get { return Z.Length; } }
        public double CellArea { get { return Step * Step; } }

        public int Index(int ix, int iy) { return iy * Nx + ix; }
        public int Ix(int index) { return index % Nx; }
        public int Iy(int index) { return index / Nx; }

        public double CellX(int ix) { return OriginX + ix * Step; }
        public double CellY(int iy) { return OriginY + iy * Step; }
        public Pt Center(int index) { return new Pt(CellX(Ix(index)), CellY(Iy(index))); }

        public bool HasData(int index) { return !double.IsNaN(Z[index]); }
        public bool Inside(int ix, int iy) { return ix >= 0 && iy >= 0 && ix < Nx && iy < Ny; }

        /// <summary>Ячейка, в которую попала мировая точка. False - точка вне сетки.</summary>
        public bool TryLocate(double x, double y, out int ix, out int iy)
        {
            ix = (int)Math.Round((x - OriginX) / Step);
            iy = (int)Math.Round((y - OriginY) / Step);
            if (Inside(ix, iy)) return true;
            ix = -1; iy = -1;
            return false;
        }
    }
}
