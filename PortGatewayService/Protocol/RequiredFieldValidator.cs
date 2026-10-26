using System.Collections.Generic;

namespace PortTraining.PortGatewayService.Protocol
{
    // Minimal required-field contract check (V1.1 section 15: CONTRACT_VALIDATION_FAILED
    // is "字段或枚举不合法"). This only checks presence/non-blank and, via RequireOneOf,
    // enumeration membership - it does not validate formats (UUIDs, hashes) or
    // cross-field rules. See PortGatewayService/README.md known limitations.
    public sealed class RequiredFieldValidator
    {
        private readonly List<string> missingFields = new List<string>();

        public RequiredFieldValidator Require(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missingFields.Add(fieldName);
            }

            return this;
        }

        public RequiredFieldValidator RequireOneOf(string value, string fieldName, params string[] allowedValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missingFields.Add(fieldName);
                return this;
            }

            foreach (var allowed in allowedValues)
            {
                if (value == allowed) return this;
            }

            missingFields.Add(fieldName);
            return this;
        }

        public RequiredFieldValidator RequirePositive(long? value, string fieldName)
        {
            if (value == null || value <= 0)
            {
                missingFields.Add(fieldName);
            }

            return this;
        }

        public bool IsValid => missingFields.Count == 0;

        public IReadOnlyList<string> MissingFields => missingFields;
    }
}
