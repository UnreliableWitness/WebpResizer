namespace WebpResizer;

/// <summary>An encoded WebP image.</summary>
/// <param name="Data">The WebP file bytes.</param>
/// <param name="Width">Output width in pixels.</param>
/// <param name="Height">Output height in pixels.</param>
public sealed record WebpResult(byte[] Data, int Width, int Height);
