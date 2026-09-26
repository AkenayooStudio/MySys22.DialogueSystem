using System;

namespace MySys22.DialogueEngine.Core
{

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class DialogueActionAttribute : Attribute
    {

        public string Id { get; }

        public string Description { get; set; }

        public string[] RequiredParameters { get; set; } = Array.Empty<string>();

        public string[] OptionalParameters { get; set; } = Array.Empty<string>();

        public bool WaitsForCompletion { get; set; }

        public bool CompletesExternally { get; set; }

        public DialogueActionAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Action id must not be empty.", nameof(id));
            Id = id.Trim();
        }
    }
}
