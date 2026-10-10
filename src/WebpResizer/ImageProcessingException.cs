namespace WebpResizer;

/// <summary>The input could not be decoded, was too large, or could not be encoded.</summary>
public sealed class ImageProcessingException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ImageProcessingException(string message, Exception? inner = null) : base(message, inner) { }
}
