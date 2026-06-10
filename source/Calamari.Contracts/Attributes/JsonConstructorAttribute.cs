namespace Octopus.Calamari.Contracts.Attributes;

// We include this as a shim so w don't take a dep on any Serialization libraries that may get out of sync.
// NewtownSoft looks up a JsonConstructor attribute by name only, so we just create a placeholder with that name 
[AttributeUsage(AttributeTargets.Constructor)]
class JsonConstructorAttribute : Attribute
{
}