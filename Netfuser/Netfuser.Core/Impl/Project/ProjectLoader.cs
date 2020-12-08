using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Base.Cil.Attributes;
using Base.Lang;
using Base.Logging;
using dnlib.DotNet;
using Netfuser.Core.Project;

namespace Netfuser.Core.Impl.Project
{
    class ProjectLoader : AbstractPlugin.Subscribed, IProjectLoader
    {
        static readonly XNamespace Msbuild2003 = "http://schemas.microsoft.com/developer/msbuild/2003";
        private static readonly string DefaultOutputPath = "bin/$(Configuration)";

        enum BuildTool
        {
            None,
            Devenv,
            Msbuild
        }

        private readonly ProjectOptions _options;
        private readonly string _builderPath;
        private readonly Version _msbuildVersion;
        private readonly BuildTool _buildTool;
        private readonly Project _root;
        private readonly Dictionary<string, Project> _allProjects;
        private readonly HashSet<string> _allReferences, _allPackageReferences;

        public ProjectOptions Options => _options;
        public string BuildConfiguration => _options.Configuration ?? "Release";
        
        public ProjectLoader(IContextImpl context, string path, ProjectOptions options)
            : base(context)
        {
            _options = options ?? new ProjectOptions();
            if (_options.Configuration == null)
                _options.Configuration = Context.DebugMode ? "Debug" : "Release";
            _allProjects = new Dictionary<string, Project>();
            _allReferences = new HashSet<string>();
            _allPackageReferences = new HashSet<string>();


            if (File.Exists(path))
                _root = GetProject(path);
            else if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.csproj");
                if (files.Length == 0) throw context.Error($"no .csproj files found in {path}");
                if (files.Length > 1) throw context.Error($"multiple .csproj files found in {path}");
                _root = GetProject(files[0]);
            }
            else throw context.Error($"path does not exist: {path}");

            if (context.OutputFolder == null)
                context.OutputFolder = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(_root.AssemblyPath), NetfuserFactory.NetfuserName));

            if (_options.Building != Building.No)
            {
                var builderPath = _options.BuilderPath;
                if (string.IsNullOrEmpty(builderPath))
                {
                    // using https://github.com/Microsoft/vswhere/wiki/Find-MSBuild
                    var vswhere = Environment.ExpandEnvironmentVariables(
                        @"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe");
                    if (File.Exists(vswhere))
                    {
                        var buildTools = Utils.RunProcess(vswhere,
                                "-latest -prerelease -products * -requires Microsoft.Component.MSBuild -property installationPath")
                            .StdOut.ReadLines().FirstOrDefault();
                        if (!string.IsNullOrEmpty(buildTools) && Directory.Exists(buildTools))
                        {
                            var msbuild = Path.Combine(buildTools, @"MSBuild\Current\Bin\MSBuild.exe");
                            if (!File.Exists(msbuild))
                                msbuild = Path.Combine(buildTools, @"MSBuild\15.0\Bin\MSBuild.exe");
                            if (File.Exists(msbuild))
                            {
                                var ver = Utils.RunProcess(msbuild, "/version").StdOut.ReadLines().LastOrDefault();
                                if (Version.TryParse(ver, out _msbuildVersion))
                                {
                                    builderPath = msbuild;
                                    _buildTool = BuildTool.Msbuild;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(builderPath))
                        {
                            var devenv = Utils.RunProcess(vswhere,
                                    "-latest -prerelease -property productPath")
                                .StdOut;
                            if (File.Exists(devenv))
                            {
                                builderPath = devenv;
                                _buildTool = BuildTool.Devenv;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(builderPath) || _buildTool == BuildTool.None)
                    throw context.Error(
                        "could not find msbuild.exe or devenv.exe on this computer, please specify path in options or set Building to No");
                _builderPath = builderPath;
            }
        }


        protected override void Handle(NetfuserEvent ev)
        {
            switch (ev)
            {
                case NetfuserEvent.Initialize _:
                    var building = _options.Building;
                    if (building == Building.No) break;
                    if (building == Building.CleanBuild)
                    {
                        foreach (var p in _allProjects.Values)
                            p.RemoveOutput();
                        building = Building.Build;
                    }

                    Logger.Info($"building {_root.CsprojPath}");

                    if (Options.AutoIncrementTargetAssemblyBuildNumber)
                        _root.IncrementBuildNumber();

                    var buildOptions = new List<string> { _root.CsprojPath };
                    string cmd = null;
                    var cfg = BuildConfiguration;
                    switch (building)
                    {
                        case Building.Build:
                            cmd = "Build";
                            break;
                        case Building.Rebuild:
                            cmd = "Rebuild";
                            break;
                    }
                    var logpath = Path.Combine(Path.GetDirectoryName(_root.CsprojPath), "msbuild.log");
                    switch (_buildTool)
                    {
                        case BuildTool.Devenv:
                            buildOptions.Add("/" + cmd);
                            buildOptions.Add("/" + cfg);
                            buildOptions.Add("/out " +logpath);
                            break;
                        case BuildTool.Msbuild:
                            buildOptions.Add("-t:" + cmd);
                            buildOptions.Add("-p:Configuration=" + cfg);
                            buildOptions.Add("-restore");
                            buildOptions.Add("-fileLogger");
                            buildOptions.Add("-fileLoggerParameters:LogFile=" + logpath);
                            if (_options.MaxCpuCount > 0)
                                buildOptions.Add("/maxcpucount:" + _options.MaxCpuCount);
                            break;
                    }

                    var be = new ProjectEvents.Build(Context, _root)
                    {
                        BuilderPath = _builderPath,
                        BuilderArguments = buildOptions,
                    };
                    Context.Fire(be);
                    var res = Utils.RunProcess(be.BuilderPath, be.BuilderArguments.ToArray());
                    if (res.ExitCode != 0)
                        Context.Error("build failed");
                    break;
                case NetfuserEvent.LoadSourceModules lsm:
                    lsm.Sources.Add(ModuleDefMD.Load(_root.AssemblyPath, Context.ModuleContext));
                    break;
                case NetfuserEvent.ResolveSourceModules rsm:
                    var asmName = rsm.Module.Assembly.Name;
                    if (_allPackageReferences.Contains(asmName) && rsm.Treat != ModuleTreat.Ignore)
                        rsm.Treat = ModuleTreat.Embed;
                    break;
            }
        }

        private Project GetProject(string path)
        {
            var absPath = Path.GetFullPath(path);
            if (!_allProjects.TryGetValue(absPath, out var proj))
            {
                _allProjects.Add(absPath, proj = new Project(this, absPath));
                proj.Load();
            }

            return proj;
        }


        internal class Project : IProject
        {
            private readonly ProjectLoader _loader;
            private readonly string _csprojPath, _name, _csprojDir, _assemblyPath, _outputPath, _outputType;
            private readonly XNamespace _ns;
            private readonly Version _toolsVersion;
            private readonly XmlNamespaceManager _nsr;
            private readonly XElement _root;
            private readonly List<string> _packageReferences, _references;
            private readonly List<Project> _projectReferences;
            private readonly List<string> _targetFrameworks;
            private readonly Dictionary<string, string> _variables;
            private readonly string _targetFramework;

            public string Name => _name;
            public string CsprojPath => _csprojPath;
            public string AssemblyPath => _assemblyPath;
            public Version ToolsVersion => _toolsVersion;
            public IReadOnlyList<string> TargetFrameworks => _targetFrameworks;
            public IReadOnlyList<string> PackageReferences => _packageReferences;
            public IReadOnlyList<string> References => _references;
            public IReadOnlyList<IProject> ProjectReference => _projectReferences;

            public Project(ProjectLoader loader, string path)
            {
                loader.Logger.Debug($"loading {path}");
                _loader = loader;
                _csprojPath = path;
                _name = Path.GetFileNameWithoutExtension(_csprojPath);
                _csprojDir = Path.GetDirectoryName(path);
                _packageReferences = new List<string>();
                _references = new List<string>();
                _projectReferences = new List<Project>();
                _targetFrameworks = new List<string>();
                _variables = new Dictionary<string, string>
                {
                    ["Configuration"] = loader._options.Configuration,
                    ["Platform"] = loader._options.Platform
                };
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                    throw loader.Context.Error($"no root element in {path}");
                _ns = root.GetDefaultNamespace();
                var rootName = root.Name;
                if (rootName != _ns + "Project")
                    throw loader.Context.Error($"invalid root element, expected <Project>, got {rootName}");
                _nsr = new XmlNamespaceManager(new NameTable());
                _nsr.AddNamespace("ns", _ns.NamespaceName);
                _root = root;
                if (string.IsNullOrEmpty(_ns.NamespaceName))
                {
                    var sdk = root.Attribute("Sdk");
                    if (sdk != null)
                        if (sdk.Value != "Microsoft.NET.Sdk")
                            throw _loader.Context.Error(
                                $"unsupported project SDK, expected 'Microsoft.NET.Sdk' got {sdk.Value}");
                    var fw = XPath("PropertyGroup/TargetFramework").FirstOrDefault();
                    if (fw != null)
                        _targetFrameworks.Add(fw.Value);
                    else
                    {
                        fw = XPath("PropertyGroup/TargetFrameworks").FirstOrDefault();
                        if (fw != null)
                            _targetFrameworks.AddRange(fw.Value.Split(';'));
                    }
                }
                else if (_ns == Msbuild2003)
                {
                    var toolsVersion = root.Attribute("ToolsVersion");
                    if (toolsVersion == null)
                        throw loader.Context.Error($"unsupported project file format: {path}");
                    _toolsVersion = Version.Parse(toolsVersion.Value);
                }
                else throw loader.Context.Error($"unsupported project file format: {path}");

                if (_targetFrameworks.Count == 1)
                    _targetFramework = _targetFrameworks[0];
                else if (_targetFrameworks.Count > 1)
                    _targetFramework =
                        _targetFrameworks.FirstOrDefault(f => f == _loader._options.TargetFramework) ??
                        _targetFrameworks.First();
                if (_targetFramework != null)
                    _variables["TargetFramework"] = _targetFramework;

                _outputPath = Expand(XPath("//ns:OutputPath").FirstOrDefault()?.Value ?? DefaultOutputPath);
                _outputType = XPath("//ns:OutputType").FirstOrDefault()?.Value;

                var ext = ".dll";
                switch (_outputType)
                {
                    case "WinExe":
                    case "Exe":
                        if (!(_targetFramework.StartsWith("netcoreapp3.") || _targetFramework.StartsWith("net5."))) // netcoreapp executable is just a native loader without any CIL
                            ext = ".exe";
                        break;
                }

                var asmPath = Path.Combine(_csprojDir, _outputPath);
                if (_targetFramework != null)
                    asmPath = Path.Combine(asmPath, _targetFrameworks[0]);
                asmPath = Path.Combine(asmPath, _name + ext);
                _assemblyPath = asmPath;
            }
            internal void IncrementBuildNumber()
            {
                var asmVer = XPath("PropertyGroup/AssemblyVersion").FirstOrDefault();
                if (asmVer == null) return;
                var asmVersion = Version.Parse(asmVer.Value);
                var fileVer = XPath("PropertyGroup/FileVersion").FirstOrDefault();
                var newAsmVer = new Version(asmVersion.Major, asmVersion.Minor, asmVersion.Build+1, asmVersion.Revision).ToString();
                if (fileVer?.Value == asmVer.Value)
                    fileVer.Value = newAsmVer;
                asmVer.Value = newAsmVer;
                SaveCsprojChanges();
            }

            private void SaveCsprojChanges()
            {
                _root.Save(_csprojPath);
            }
            private string Expand(string macro)
            {
                if (macro == null) return null;
                var result = new StringBuilder();
                var i = 0;
                while (i < macro.Length)
                {
                    var c = macro[i++];
                    if (c == '$' && i < macro.Length && macro[i] == '(')
                    {
                        var vb = new StringBuilder();
                        var ok = false;
                        while (++i < macro.Length)
                        {
                            c = macro[i];
                            if (c == ')')
                            {
                                i++;
                                ok = true;
                                break;
                            }

                            vb.Append(c);
                        }

                        if (!ok) result.Append("$(").Append(vb);
                        else if (_variables.TryGetValue(vb.ToString(), out var v))
                            result.Append(v);
                        else result.Append("$(").Append(vb).Append(')');
                    }
                    else result.Append(c);
                }

                return result.ToString();
            }

            private bool EvalCondition(string cond)
            {
                var expanded = Expand(cond);
                var expr = new CondParser(expanded).Parse();
                var val = expr(this);
                return Convert.ToBoolean(val);
            }

            [Mark(1)]
            public bool CondFn(Ident fn, object[] args)
            {
                throw new NotImplementedException();
            }


            private IEnumerable<XElement> Filter(IEnumerable<XElement> elements)
            {
                foreach (var element in elements)
                {
                    var skip = false;
                    var el = element;
                    while (el != _root)
                    {
                        var cond = el.Attribute("Condition");
                        if (cond != null && !EvalCondition(cond.Value))
                        {
                            skip = true;
                            break;
                        }

                        el = el.Parent;
                    }

                    if (!skip) yield return element;
                }
            }

            private IEnumerable<XElement> XPath(string xpath)
                => Filter(_root.XPathSelectElements(xpath, _nsr));

            public void Load()
            {
                foreach (var pr in XPath("//ns:ProjectReference"))
                {
                    var include = pr.Attribute("Include");
                    if (include != null)
                        _projectReferences.Add(_loader.GetProject(ResolvePath(include.Value)));
                }

                foreach (var pr in XPath("//ns:PackageReference"))
                {
                    var include = pr.Attribute("Include");
                    if (include != null)
                    {
                        var p = include.Value;
                        _packageReferences.Add(p);
                        _loader._allPackageReferences.Add(p);
                    }
                }

                foreach (var pr in XPath("//ns:Reference"))
                {
                    var include = pr.Attribute("Include");
                    if (include != null)
                    {
                        var p = include.Value;
                        _references.Add(p);
                        _loader._allReferences.Add(p);
                    }
                }
            }

            public void RemoveOutput()
            {
                var dir = Path.GetDirectoryName(_assemblyPath);
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.GetFiles(dir))
                {
                    var ext = Path.GetExtension(file);
                    switch (ext)
                    {
                        case ".exe":
                        case ".dll":
                            break;
                        default:
                            continue;
                    }

                    var fi = new FileInfo(file);
                    if (_loader.Context.Fire(new ProjectEvents.RemoveFile(_loader.Context, this, fi)).Keep) continue;
                    fi.Delete();
                }
            }

            string ResolvePath(string relative)
            {
                if (Path.IsPathRooted(relative)) return Path.GetFullPath(relative);
                return Path.GetFullPath(Path.Combine(_csprojDir, relative));
            }
        }
    }
}