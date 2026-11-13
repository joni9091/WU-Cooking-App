using Android.Graphics;
using Android.Media;

namespace FridgeChef.Services;

// Turns any photo into the avatar format the database expects: square, 300 x 300, JPEG, under 200 KB.
public static class AvatarImage
{
    public const int Size = 300;
    public const int MaxBytes = 200 * 1024;

    public static byte[] ToSquareJpeg(byte[] photo)
    {
        using var source = BitmapFactory.DecodeByteArray(photo, 0, photo.Length) ?? throw new InvalidDataException("not an image");
        var side = Math.Min(source.Width, source.Height);

        // Crop the centre square, rotate as the camera intended (EXIF), and scale to 300 px in one step.
        var matrix = new Matrix();
        matrix.PostScale((float)Size / side, (float)Size / side);
        matrix.PostRotate(ExifRotation(photo));
        using var square = Bitmap.CreateBitmap(source, (source.Width - side) / 2, (source.Height - side) / 2, side, side, matrix, true)!;

        for (var quality = 90; ; quality -= 10) // lower quality until it fits (300 px almost always fits at 90)
        {
            using var output = new MemoryStream();
            square.Compress(Bitmap.CompressFormat.Jpeg!, quality, output);
            if (output.Length < MaxBytes || quality <= 30) return output.ToArray();
        }
    }

    static float ExifRotation(byte[] photo)
    {
        try
        {
            using var exif = new ExifInterface(new MemoryStream(photo));
            return exif.GetAttributeInt(ExifInterface.TagOrientation, (int)Orientation.Normal) switch
            {
                (int)Orientation.Rotate90 => 90,
                (int)Orientation.Rotate180 => 180,
                (int)Orientation.Rotate270 => 270,
                _ => 0,
            };
        }
        catch (Exception) { return 0; } // no EXIF data: keep as is
    }
}
