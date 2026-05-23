namespace PureUpdate.Core.Models;

public sealed record InstallError(
    DateTime Date,
    string   Provider,
    string   Title,
    string   ErrorMessage);
