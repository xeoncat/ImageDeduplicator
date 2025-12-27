using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

// This class contains the logic for Perceptual Hashing (specifically, the Average Hash or aHash) and calculating the Hamming Distance.
public static class ImageHasher
{
    // The size for the scaled-down image used for hashing (16x16 = 256 pixels, 256-bit hash)
    private const int HashSize = 16;
    private const int TotalPixels = HashSize * HashSize;

    // Calculates the Hamming Distance (number of differing bits) between two hashes.
    public static int CalculateHammingDistance(byte[] hash1, byte[] hash2)
    {
        // Safety check: initialize at max distance if arrays don't match or are empty
        if (hash1.Length != hash2.Length || hash1.Length == 0)
            return 256;

        int distance = 0;

        for (int i = 0; i < hash1.Count(); i++)
        {
            // XOR the bytes to find differing bits
            int xorValue = hash1[i] ^ hash2[i];

            // Count bits set to 1 in the result (Brian Kernighan's way)
            while (xorValue != 0)
            {
                xorValue &= (xorValue - 1);
                distance++;
            }
        }

        return distance;
    }

    public static (byte[] Hash, int Width, int Height) GetImageMetadata(string path)
    {
        try
        {
            string safePath = path; // 1. Strip the URI if it exists
            if (safePath.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                safePath = safePath[8..].Replace('/', '\\');
            }
            // 2. Apply the Long Path prefix for Windows API safety
            if (!safePath.StartsWith(@"\\?\"))
            {
                safePath = @"\\?\" + safePath;
            }

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var image = Image.Load<L8>(fs))
                {
                    int originalWidth = image.Width;
                    int originalHeight = image.Height;

                    image.Mutate(x => x.Resize(HashSize, HashSize)); // Standard Perceptual Hash logic (resize to 16px)

                    byte[] hash = new byte[TotalPixels / 8];
                    long sum = 0;
                    for (int y = 0; y < HashSize; y++)
                    {
                        for (int x = 0; x < HashSize; x++)
                        {
                            sum += image[x, y].PackedValue;
                        }
                    }

                    long average = sum / TotalPixels;
                    for (int i = 0; i < TotalPixels; i++)
                    {
                        if (image[i % HashSize, i / HashSize].PackedValue >= average)
                        {
                            hash[i / 8] |= (byte)(1 << (i % 8)); // Initialize bits in the byte array
                        }
                    }
                    return (hash, originalWidth, originalHeight);
                }
            }
        }
        catch { return (Array.Empty<byte>(), 0, 0); }
    }
}
