namespace Backend.Application.Common.Exceptions;

public sealed class OperationConflictException(string code) : Exception(code);
