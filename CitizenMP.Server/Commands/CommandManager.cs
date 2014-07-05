using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CitizenMP.Server.Commands
{
    public class CommandManager
    {
        internal Game.GameServer GameServer { get; private set; }

        private static Dictionary<string, MethodInfo> ms_consoleCommands = new Dictionary<string, MethodInfo>();

        static CommandManager()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();

            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (var method in methods)
                {
                    var attribute = method.GetCustomAttribute<ConsoleCommandAttribute>();

                    if (attribute != null)
                    {
                        ms_consoleCommands.Add(attribute.CommandName.ToLowerInvariant(), method);
                    }
                }
            }
        }

        public bool HandleCommand(string commandName, IEnumerable<string> args)
        {
            MethodInfo handler;

            if (ms_consoleCommands.TryGetValue(commandName.ToLowerInvariant(), out handler))
            {
                handler.Invoke(null, new object[] { this, commandName, args.ToArray() });

                return true;
            }

            return false;
        }

        internal void SetGameServer(Game.GameServer gameServer)
        {
            if (GameServer != null)
            {
                throw new InvalidOperationException("This CommandManager is already associated with a GameServer.");
            }

            GameServer = gameServer;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class ConsoleCommandAttribute : Attribute
    {
        public ConsoleCommandAttribute(string commandName)
        {
            CommandName = commandName;
        }

        public string CommandName { get; private set; }
    }
}
