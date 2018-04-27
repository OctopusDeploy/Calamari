namespace Calamari.Util
{
    public interface IPluginUtils
    {
        /// <summary>
        /// Returns the first argument from those passed to the entry method
        /// </summary>
        /// <param name="args">args passed to the entry method</param>
        /// <returns>The sanitised first argument</returns>
        string GetFirstArgument(string[] args);
    }
}
