namespace Backend.Application.Common.Exceptions;

public sealed class EntityNotFoundException(string code) : Exception(code);
