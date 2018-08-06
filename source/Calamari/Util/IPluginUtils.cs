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

        /// <summary>
        /// Returns the second argument from those passed to the entry method.
        /// This is significant if the first command identifies the help command to be run,
        /// as the second command is the name of the command to get help for.
        /// </summary>
        /// <param name="args">args passed to the entry method</param>
        /// <returns>The sanitised second argument</returns>
        string GetSecondArgument(string[] args);
    }
}
