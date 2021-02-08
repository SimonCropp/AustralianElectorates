﻿using System.Diagnostics;
using System.Drawing;
using System.IO;

public static class PdfToPng
{
    // Change as needed if not in local path
    const string pngquantPath = @"C:\pngquant";
    const string ghostScriptPath = @"C:\Program Files\gs\gs9.27\bin";

    public static string Convert(string pdf)
    {
        var tempPng1 = pdf.Replace(".pdf", "_temp1.png");
        var tempPng2 = pdf.Replace(".pdf", "_temp2.png");
        var png = Path.ChangeExtension(pdf, "png");
        File.Delete(png);
        File.Delete(tempPng1);
        File.Delete(tempPng2);
        CallGhostScript(pdf, tempPng1);

        var cropSize = 60;
        using (var bmpImage = (Bitmap) Image.FromFile(tempPng1))
        {
            var size = bmpImage.Size;
            Rectangle cropRect = new(cropSize, cropSize, size.Width - 2 * cropSize, size.Height - 2 * cropSize);

            using (var bitmap = bmpImage.Clone(cropRect, bmpImage.PixelFormat))
            {
                DrawBitmapWithBorder(bitmap);
                bitmap.Save(tempPng2);
            }
        }

        File.Delete(tempPng1);
        CallPngquant(tempPng2, png);
        return png;
    }

    static void DrawBitmapWithBorder(Bitmap bitmap)
    {
        using var graphics = Graphics.FromImage(bitmap);
        using Pen pen = new(Brushes.Black, 3);
        graphics.DrawLine(pen, new(0, 0), new(0, bitmap.Height));
        graphics.DrawLine(pen, new(0, 0), new(bitmap.Width, 0));
        graphics.DrawLine(pen, new(0, bitmap.Height), new(bitmap.Width, bitmap.Height));
        graphics.DrawLine(pen, new(bitmap.Width, 0), new(bitmap.Width, bitmap.Height));
    }

    static void CallPngquant(string tempPng, string png)
    {
        ProcessStartInfo pngquant = new()
        {
            FileName = "pngquant.exe",
            Arguments = $"--force --verbose --ordered --speed=1 --skip-if-larger --quality=50-70 {tempPng} --output {png}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        EnvironmentHelpers.AppendToPath(pngquantPath);
        using var process = Process.Start(pngquant)!;
        process.WaitForExit();
        //skip-if-larger can result in 98 "not saved"
        if (process.ExitCode == 98)
        {
            File.Move(tempPng, png);
        }
        else
        {
            File.Delete(tempPng);
        }
    }

    static void CallGhostScript(string pdf, string tempPng)
    {
        ProcessStartInfo gswin64 = new()
        {
            FileName = "gswin64c.exe",
            Arguments = $"-dNoCancel -sDEVICE=png16m -dBATCH -r300 -dNOPAUSE -dDownScaleFactor=2 -q -sOutputFile={tempPng} {pdf}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        EnvironmentHelpers.AppendToPath(ghostScriptPath);
        using var process = Process.Start(gswin64)!;
        process.WaitForExit();
    }
}