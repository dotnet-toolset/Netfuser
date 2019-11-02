using System.Collections.Generic;
using System.IO;

namespace Netfuser.Core.Project
{
    public abstract class ProjectEvents : NetfuserEvent
    {
        public readonly IProject Project;

        protected ProjectEvents(IContext context, IProject project)
            : base(context)
        {
            Project = project;
        }

        public class Build : ProjectEvents
        {
            public string BuilderPath;
            public List<string> BuilderArguments;

            public Build(IContext context, IProject project) 
                : base(context, project)
            {
            }
        }

        public class RemoveDirectory : ProjectEvents
        {
            public readonly DirectoryInfo Directory;
            public bool Keep;

            internal RemoveDirectory(IContext context, IProject project, DirectoryInfo directory)
                : base(context, project)
            {
                Directory = directory;
            }
        }

        public class RemoveFile : ProjectEvents
        {
            public readonly FileInfo File;
            public bool Keep;

            internal RemoveFile(IContext context, IProject project, FileInfo file)
                : base(context, project)
            {
                File = file;
            }
        }
    }
}