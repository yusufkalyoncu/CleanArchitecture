using System.Reflection;
using CleanArchitecture.Application.Users.Register;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure;
using CleanArchitecture.WebApi.Middleware;

namespace CleanArchitecture.Architecture.Tests;

public static class Assemblies
{
    public static readonly Assembly Domain = typeof(User).Assembly;
    public static readonly Assembly Application = typeof(UserRegisterCommand).Assembly;
    public static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;
    public static readonly Assembly WebApi = typeof(RequestContextLoggingMiddleware).Assembly;
}