using System;
using System.Drawing;
using Microsoft.Win32;

namespace MatrixScreensaver
{
    /// <summary>User-adjustable options, persisted in HKCU\Software\MatrixScreensaver.</summary>
    internal sealed class Settings
    {
        private const string KeyPath = @"Software\MatrixScreensaver";

        public const int MinSpeed = 1, MaxSpeed = 100;
        public const int MinCharSize = 8, MaxCharSize = 72;
        public const int MinDensity = 1, MaxDensity = 100;
        public const int MinTrailTime = 3, MaxTrailTime = 300; // tenths of a second
        public const int MinCharWidth = 50, MaxCharWidth = 200;
        public const int MinColumnSpacing = 0, MaxColumnSpacing = 300;
        public const int MinStrokeWeight = 0, MaxStrokeWeight = 10;
        public const int MinScanlineStrength = 10, MaxScanlineStrength = 100;
        public const int MinImageStrength = 5, MaxImageStrength = 100;

        public static readonly Color DefaultColor = Color.FromArgb(0, 255, 70);

        public Color Color = DefaultColor;
        public bool Rainbow = false;
        public int Speed = 40;          // 1..100
        public int CharSize = 18;       // pixels at 100% scaling
        public int CharWidth = 100;     // horizontal glyph stretch, percent
        public int ColumnSpacing = 100; // gap between columns, percent of character size
        public int StrokeWeight = 4;    // 0 = font's natural weight .. 10 = heavy
        public bool Scanlines = false;  // CRT-style horizontal lines
        public int ScanlineStrength = 50; // percent darkening of the scanline rows
        public bool HiddenImage = false;  // subtly reveal images in the rain
        public string ImagePath = "";     // an image file or a folder of images
        public int ImageStrength = 35;    // how visible the hidden image is, percent
        public int Density = 50;        // 1..100
        public int TrailTime = 30;      // tenths of a second a character stays visible after the head passes
        public bool GlowHead = true;    // bright white leading character
        public bool AllMonitors = true; // false = rain on primary only, others go black

        public Settings Clone() => (Settings)MemberwiseClone();

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (key == null) return s;
                    s.Color = Color.FromArgb(255, Color.FromArgb(ReadInt(key, "Color", DefaultColor.ToArgb())));
                    s.Rainbow = ReadInt(key, "Rainbow", 0) != 0;
                    s.Speed = Clamp(ReadInt(key, "Speed", s.Speed), MinSpeed, MaxSpeed);
                    s.CharSize = Clamp(ReadInt(key, "CharSize", s.CharSize), MinCharSize, MaxCharSize);
                    s.CharWidth = Clamp(ReadInt(key, "CharWidth", s.CharWidth), MinCharWidth, MaxCharWidth);
                    s.ColumnSpacing = Clamp(ReadInt(key, "ColumnSpacing", s.ColumnSpacing), MinColumnSpacing, MaxColumnSpacing);
                    s.StrokeWeight = Clamp(ReadInt(key, "StrokeWeight", s.StrokeWeight), MinStrokeWeight, MaxStrokeWeight);
                    s.Scanlines = ReadInt(key, "Scanlines", 0) != 0;
                    s.ScanlineStrength = Clamp(ReadInt(key, "ScanlineStrength", s.ScanlineStrength), MinScanlineStrength, MaxScanlineStrength);
                    s.HiddenImage = ReadInt(key, "HiddenImage", 0) != 0;
                    s.ImagePath = key.GetValue("ImagePath") as string ?? "";
                    s.ImageStrength = Clamp(ReadInt(key, "ImageStrength", s.ImageStrength), MinImageStrength, MaxImageStrength);
                    s.Density = Clamp(ReadInt(key, "Density", s.Density), MinDensity, MaxDensity);
                    s.TrailTime = Clamp(ReadInt(key, "TrailTime", s.TrailTime), MinTrailTime, MaxTrailTime);
                    s.GlowHead = ReadInt(key, "GlowHead", 1) != 0;
                    s.AllMonitors = ReadInt(key, "AllMonitors", 1) != 0;
                }
            }
            catch (Exception)
            {
                // Corrupt or inaccessible settings: fall back to defaults.
            }
            return s;
        }

        public void Save()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                key.SetValue("Color", Color.ToArgb(), RegistryValueKind.DWord);
                key.SetValue("Rainbow", Rainbow ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("Speed", Speed, RegistryValueKind.DWord);
                key.SetValue("CharSize", CharSize, RegistryValueKind.DWord);
                key.SetValue("CharWidth", CharWidth, RegistryValueKind.DWord);
                key.SetValue("ColumnSpacing", ColumnSpacing, RegistryValueKind.DWord);
                key.SetValue("StrokeWeight", StrokeWeight, RegistryValueKind.DWord);
                key.SetValue("Scanlines", Scanlines ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("ScanlineStrength", ScanlineStrength, RegistryValueKind.DWord);
                key.SetValue("HiddenImage", HiddenImage ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("ImagePath", ImagePath ?? "", RegistryValueKind.String);
                key.SetValue("ImageStrength", ImageStrength, RegistryValueKind.DWord);
                key.SetValue("Density", Density, RegistryValueKind.DWord);
                key.SetValue("TrailTime", TrailTime, RegistryValueKind.DWord);
                key.SetValue("GlowHead", GlowHead ? 1 : 0, RegistryValueKind.DWord);
                key.SetValue("AllMonitors", AllMonitors ? 1 : 0, RegistryValueKind.DWord);
            }
        }

        private static int ReadInt(RegistryKey key, string name, int fallback) =>
            key.GetValue(name) is int v ? v : fallback;

        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
