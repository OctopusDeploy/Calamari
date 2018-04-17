namespace Calamari.Integration.Scripting
{
    /// <summary>
    /// Script engines can be decorated to provide things such as additional environment
    /// variables (AWS credentials being an example), or to wrap up the execution of the
    /// supplied script with another script (Azure logins are an example of this).
    ///
    /// Because a single script might span multiple tools and cloud providers, decorators
    /// around the script engine give us the flexibility to mix and match requirements.
    /// </summary>
    public interface IScriptEngineDecorator : IScriptEngine
    {
        /// <summary>
        /// The parent script engine wrapped by this decorator
        /// </summary>
        IScriptEngine Parent { get; set;  }         
        
        /// <summary>
        /// The name of the decorator
        /// </summary>
        string Name { get; }
    }
}