namespace Cliopack.Clio;

// Message is user-readable and safe to show as is.
public class ClioException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class ClioNotFoundException(Exception inner)
    : ClioException("clio is not installed or not on PATH. Install it: dotnet tool install clio -g", inner);
