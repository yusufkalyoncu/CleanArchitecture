namespace CleanArchitecture.Domain.Audit;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditMaskAttribute : Attribute;