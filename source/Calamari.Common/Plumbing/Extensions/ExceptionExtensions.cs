using System;
using System.Text;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class ExceptionExtensions
    {
        public static string PrettyPrint(this Exception ex, StringBuilder? sb = null)
        {
            sb ??= new StringBuilder();

            sb.AppendLine(ex.Message);
            sb.AppendLine(ex.GetType().FullName);
            sb.AppendLine(ex.StackTrace);

            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("--Inner Exception--");
                PrettyPrint(ex.InnerException, sb);
            }

            return sb.ToString();
        }
    }
}