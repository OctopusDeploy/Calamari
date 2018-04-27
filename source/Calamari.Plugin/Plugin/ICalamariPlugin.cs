namespace Calamari.Plugin
{
    public interface ICalamariPlugin
    {
        /// <summary>
        /// The plugin name
        /// </summary>
        string Name { get; }
        /// <summary>
        /// The plugin group
        /// </summary>
        string Group { get; }
    }
}
