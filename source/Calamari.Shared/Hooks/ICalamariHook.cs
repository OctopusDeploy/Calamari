namespace Calamari.Hooks
{
    /// <summary>
    /// Hooks contribute functionality to specific parts of the pipeline. They are lower
    /// level than conventions, and have a small number of specific implementations.
    /// </summary>
    public interface ICalamariHook
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
