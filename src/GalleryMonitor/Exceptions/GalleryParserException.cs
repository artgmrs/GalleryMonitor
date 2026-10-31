namespace GalleryMonitor.Exceptions;

public sealed class GalleryParserException(string message, Exception? innerException = null) : Exception(message, innerException);
