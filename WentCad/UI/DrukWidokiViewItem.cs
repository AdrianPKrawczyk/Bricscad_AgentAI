namespace WentCad.UI
{
    public class DrukWidokiViewItem
    {
        public string ViewId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Type) ? Name : $"{Name} ({Type})";
    }
}
