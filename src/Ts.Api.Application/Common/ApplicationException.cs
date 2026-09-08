namespace Ts.Api.Application.Common;

public abstract class ApplicationException(string message) : Exception(message);

public sealed class ResourceNotFoundException(string message) : ApplicationException(message);

public sealed class ConflictException(string message) : ApplicationException(message);

public sealed class PreconditionFailedException(string message) : ApplicationException(message);
