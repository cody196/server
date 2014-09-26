using System;
using System.Runtime.CompilerServices;

using CitizenMP.Server.Logging;

namespace CitizenMP.Server
{
    public static class LogExtensions
    {
        public static BaseLog Log<T>(this T type,
                                     [CallerMemberName] string memberName = "", 
                                     [CallerFilePath]   string sourceFilePath = "", 
                                     [CallerLineNumber] int sourceLineNumber = 0)
        {
            return new BaseLog(typeof(T).FullName, memberName, sourceFilePath, sourceLineNumber);
        }
    }
}