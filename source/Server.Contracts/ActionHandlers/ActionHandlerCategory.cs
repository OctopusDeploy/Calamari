using System;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class ActionHandlerCategory : IEquatable<ActionHandlerCategory>
    {
        public static readonly ActionHandlerCategory BuiltInStep = new ActionHandlerCategory("BuiltInStep", "Built-in Steps", 9000);
        public static readonly ActionHandlerCategory Script = new ActionHandlerCategory("Script", "Script", 200);
        public static readonly ActionHandlerCategory Package = new ActionHandlerCategory("Package", "Package", 300);
        public static readonly ActionHandlerCategory Terraform = new ActionHandlerCategory("Terraform", "Terraform", 1100);
        public static readonly ActionHandlerCategory Azure = new ActionHandlerCategory("Azure", "Azure", 500);
        public static readonly ActionHandlerCategory Atlassian = new ActionHandlerCategory("Atlassian", "Atlassian", 1300);

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
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ActionHandlerCategory) obj);
        }

        public override int GetHashCode()
            => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

        public static bool operator ==(ActionHandlerCategory left, ActionHandlerCategory right)
            => Equals(left, right);

        public static bool operator !=(ActionHandlerCategory left, ActionHandlerCategory right)
            => !Equals(left, right);
    }
}
