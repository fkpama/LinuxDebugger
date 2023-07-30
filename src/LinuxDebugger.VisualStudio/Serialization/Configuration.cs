
using Newtonsoft.Json;

namespace LinuxDebugger.VisualStudio.Serialization
{
    /// <summary>Launch Configuration attributes.</summary>
    /// <remarks>https://code.visualstudio.com/docs/editor/debugging#_launchjson-attributes</remarks>
    public sealed class Configuration
    {
        public Configuration(string adapterPath, string args)
        {
            this.Adapter = adapterPath;
            this.AdapterArgs = args;
        }

        //// MANDITORY ATTRIBUTES ----------------

        /// <summary>The reader-friendly name to appear in the Debug launch configuration dropdown.</summary>
        public string Name { get; set; } = "Debug on Linux";

        /// <summary>The type of debugger to use for this launch configuration. (i.e. 'coreclr' for .NET 3.1/5/6, 'clr' for .NET Framework).</summary>
        public string Type { get; set; } = "coreclr";

        /// <summary>The request type of this launch configuration. Currently, launch and attach are supported.</summary>
        public string Request { get; set; } = "launch";

        //// OPTIONAL ATTRIBUTES ----------------

        /// <summary>Executable or file to run when launching the debugger.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? Program { get; set; }

        /// <summary>Arugments passed to the program to debug.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(ArgumentsConverter))]
        public List<string>? Args { get; set; }

        /// <summary>Current working directory for finding dependencies and other files.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? Cwd { get; set; }

        /// <summary>Break immediately when the program launches.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool StopAtEntry { get; set; }

        /// <summary>What kind of console to use. For example, 'internalConsole', 'integratedTerminal', or 'externalTerminal'.</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Console => "integratedTerminal"; // "internalConsole";
        //public string Console => "externalTerminal";

        /// <summary>Environment variables (the value null can be used to "undefine" a variable).</summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(EnvironmentVariableConverter))]
        public IReadOnlyDictionary<string, string>? Env { get; set; }

        [JsonProperty("$adapter")]
        public string Adapter { get; set; }

        [JsonProperty("$adapterArgs")]
        public string AdapterArgs { get; set; }

        //// ---- TESTING PHASE BELOW ----
        ////
        //// /// <summary>Program name to debug (i.e. 'TestApp.exe').</summary>
        //// [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        //// public string ProcessName { get; set; }
        //// 
        //// [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        //// public PipeTransport PipeTransport { get; set; }
    }

}