using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class ActionHandlerCategory : IEquatable<ActionHandlerCategory>
    {
        public static readonly ActionHandlerCategory BuiltInStep = new("BuiltInStep", "Built-in Steps", 9000);
        public static readonly ActionHandlerCategory Script = new("Script", "Script", 200);
        public static readonly ActionHandlerCategory Package = new("Package", "Package", 300);
        public static readonly ActionHandlerCategory Terraform = new("Terraform", "Terraform", 1100);
        public static readonly ActionHandlerCategory Azure = new("Azure", "Azure", 500);
        public static readonly ActionHandlerCategory Atlassian = new("Atlassian", "Atlassian", 1300);

        public ActionHandlerCategory(string id, string name, int displayOrder)
        {
            Id = id;
            Name = name;
            DisplayOrder = displayOrder;
        }

        public string Id { get; }
        public string Name { get; }
        public int DisplayOrder { get; }

        public bool Equals(ActionHandlerCategory? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ActionHandlerCategory)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Id);
        }

        public static bool operator ==(ActionHandlerCategory left, ActionHandlerCategory right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ActionHandlerCategory left, ActionHandlerCategory right)
        {
            return !Equals(left, right);
        }
    }
}