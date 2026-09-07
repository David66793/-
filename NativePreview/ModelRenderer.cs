using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Hearthhold.Core;

namespace Hearthhold.Preview
{
    public sealed partial class GameWindow
    {
        private sealed class ModelSprite : IDisposable
        {
            public Bitmap Image;
            public RectangleF Bounds;
            public void Dispose() { Image.Dispose(); }
        }
        private sealed class ProjectedFace
        {
            public PointF[] Points;
            public Color Color;
            public float Depth;
        }
        private readonly Dictionary<string, ModelSprite> modelSprites = new Dictionary<string, ModelSprite>();
        private static PointF ModelProject(ModelPoint p) { return new PointF((p.X - p.Z) * 22, (p.X + p.Z) * 11 - p.Y * 27); }
        private ModelSprite SpriteFor(string key, ModelMesh mesh)
        {
            ModelSprite result;
            if (modelSprites.TryGetValue(key, out result)) return result;
            List<ProjectedFace> faces = new List<ProjectedFace>();
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (ModelFace face in mesh.Faces)
            {
                ModelPoint n = face.Normal;
                if (n.X + n.Z + n.Y * 0.815f <= 0.001f) continue;
                PointF[] points = new PointF[face.Points.Length]; float depth = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    ModelPoint p = face.Points[i]; points[i] = ModelProject(p);
                    minX = Math.Min(minX, points[i].X); maxX = Math.Max(maxX, points[i].X);
                    minY = Math.Min(minY, points[i].Y); maxY = Math.Max(maxY, points[i].Y);
                    depth += p.X + p.Z + p.Y * 0.815f;
                }
                float light = 0.73f + Math.Max(0, n.X * -0.4f + n.Y * 0.83f + n.Z * 0.42f) * 0.33f;
                int rgb = face.Color;
                Color color = Color.FromArgb(Math.Min(255, (int)((rgb >> 16 & 255) * light)), Math.Min(255, (int)((rgb >> 8 & 255) * light)), Math.Min(255, (int)((rgb & 255) * light)));
                faces.Add(new ProjectedFace { Points = points, Color = color, Depth = depth / points.Length });
            }
            minX = (float)Math.Floor(minX) - 3; minY = (float)Math.Floor(minY) - 3;
            int width = (int)Math.Ceiling(maxX - minX) + 6, height = (int)Math.Ceiling(maxY - minY) + 6;
            Bitmap image = new Bitmap(width * 2, height * 2);
            faces.Sort(delegate(ProjectedFace a, ProjectedFace b) { return a.Depth.CompareTo(b.Depth); });
            using (Graphics g = Graphics.FromImage(image))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(2, 2); g.TranslateTransform(-minX, -minY);
                foreach (ProjectedFace face in faces)
                {
                    using (Brush brush = new SolidBrush(face.Color)) g.FillPolygon(brush, face.Points);
                    using (Pen pen = new Pen(Color.FromArgb(50, Shade(face.Color, -27)), 0.25f)) g.DrawPolygon(pen, face.Points);
                }
            }
            result = new ModelSprite { Image = image, Bounds = new RectangleF(minX, minY, width, height) };
            modelSprites.Add(key, result); return result;
        }
        private ModelSprite BuildingSprite(BuildingKind kind, int level)
        {
            string key = "building:" + kind + ":" + level; ModelSprite sprite;
            return modelSprites.TryGetValue(key, out sprite) ? sprite : SpriteFor(key, ModelFactory.Building(kind, level));
        }
        private ModelSprite TroopSprite(TroopKind kind)
        {
            string key = "troop:" + kind; ModelSprite sprite;
            return modelSprites.TryGetValue(key, out sprite) ? sprite : SpriteFor(key, ModelFactory.Troop(kind));
        }
        private void PaintSprite(Graphics g, ModelSprite sprite, float x, float y, float scale)
        {
            RectangleF b = sprite.Bounds;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(sprite.Image, new RectangleF(x + b.X * scale, y + b.Y * scale, b.Width * scale, b.Height * scale));
        }
        private void PaintModelBuilding(Graphics g, Building b)
        {
            PointF origin = Project(b.X, b.Z, 0);
            PaintSprite(g, BuildingSprite(b.Kind, b.Level), origin.X, origin.Y, zoom);
        }
        private void PaintModelIcon(Graphics g, BuildingKind kind, RectangleF rect)
        {
            ModelSprite sprite = BuildingSprite(kind, 1);
            float scale = Math.Min(rect.Width / sprite.Bounds.Width, rect.Height / sprite.Bounds.Height);
            g.DrawImage(sprite.Image, rect.X + (rect.Width - sprite.Bounds.Width * scale) / 2, rect.Y + (rect.Height - sprite.Bounds.Height * scale) / 2, sprite.Bounds.Width * scale, sprite.Bounds.Height * scale);
        }
        private void PaintTroopPortrait(Graphics g, TroopKind kind, RectangleF rect)
        {
            ModelSprite sprite = TroopSprite(kind);
            float scale = Math.Min(rect.Width / sprite.Bounds.Width, rect.Height / sprite.Bounds.Height);
            g.DrawImage(sprite.Image, rect.X + (rect.Width - sprite.Bounds.Width * scale) / 2, rect.Y + (rect.Height - sprite.Bounds.Height * scale) / 2, sprite.Bounds.Width * scale, sprite.Bounds.Height * scale);
        }
    }
}
