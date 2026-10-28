namespace CustomerPortalAPI.Services
{
    public class ContentStatusCatalog
    {
        private readonly Dictionary<string, string[]> _statuses;

        public ContentStatusCatalog(IConfiguration configuration)
        {
            _statuses = configuration.GetSection("ContentStatuses")
                .GetChildren()
                .ToDictionary(
                    section => section.Key,
                    section => section.Get<string[]>() ?? Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public string[] GetByWorkflow(string? workflow)
        {
            if (string.IsNullOrWhiteSpace(workflow))
                return Array.Empty<string>();

            if (_statuses.TryGetValue(workflow, out var exact))
                return exact;

            if (_statuses.TryGetValue("Status - " + workflow.Trim(), out var prefixed))
                return prefixed;

            return Array.Empty<string>();
        }
    }
}
