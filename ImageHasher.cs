using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

// This class contains the logic for Perceptual Hashing (specifically, the Average Hash or aHash) and calculating the Hamming Distance.
public static class ImageHasher
{
    // The size for the scaled-down image used for hashing (8x8 = 64 pixels, 64-bit hash)
    private const int HashSize = 8;
    private const int TotalPixels = HashSize * HashSize;

    /* Computes the 64-bit Average Hash (aHash) for an image file.
    public static ulong ComputeAverageHash(string path)
    {
        if (!File.Exists(path)) return 0;

        try
        {
            using (Image<Rgba32> image = Image.Load<Rgba32>(path))
            {
                // 1. Resize and Grayscale: Reduce to 8x8 pixels and convert to grayscale.
                image.Mutate(x => x
                    .Resize(HashSize, HashSize)
                    .Grayscale());

                // 2. Calculate Average: Find the average pixel luminance value.
                long totalLuminance = 0;
                for (int y = 0; y < HashSize; y++)
                {
                    for (int x = 0; x < HashSize; x++)
                    {
                        totalLuminance += image[x, y].R; // R channel holds the grayscale luminance
                    }
                }
                double average = (double)totalLuminance / TotalPixels;

                // 3. Compute Hash: Create the 64-bit hash.
                ulong hash = 0;
                int bit = 0;
                for (int y = 0; y < HashSize; y++)
                {
                    for (int x = 0; x < HashSize; x++)
                    {
                        // Set bit to 1 if the pixel is brighter than the average
                        if (image[x, y].R > average)
                        {
                            hash |= (1UL << bit);
                        }
                        bit++;
                    }
                }
                return hash;
            }
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            // Handle files that ImageSharp cannot process
            System.Diagnostics.Debug.WriteLine($"Skipped non-image file: {path}");
            return 0;
        }
        catch
        {
            // Catch other IO or general errors
            return 0;
        }
    }*/

    // Calculates the Hamming Distance (number of differing bits) between two hashes.
    public static int CalculateHammingDistance(ulong hash1, ulong hash2)
    {
        ulong xorValue = hash1 ^ hash2;
        int distance = 0;

        // Count the number of set bits (population count)
        while (xorValue != 0)
        {
            xorValue &= (xorValue - 1);
            distance++;
        }

        return distance;
    }

    public static (ulong Hash, int Width, int Height) GetImageMetadata(string path)
    {
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var image = Image.Load<L8>(fs))
                {
                    int originalWidth = image.Width;
                    int originalHeight = image.Height;

                    image.Mutate(x => x.Resize(8, 8)); // Standard Perceptual Hash logic (resize to 8x8)

                    ulong hash = 0;
                    long sum = 0;
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            sum += image[x, y].PackedValue;
                        }
                    }

                    long average = sum / 64;
                    for (int i = 0; i < 64; i++)
                    {
                        if (image[i % 8, i / 8].PackedValue >= average)
                        {
                            hash |= (1UL << i);
                        }
                    }

                    return (hash, originalWidth, originalHeight);
                }
            }
        }
        catch { return (0, 0, 0); }
    }
}
