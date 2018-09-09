using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;
using Utils = Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace Confuser
{
    class MSBuildLogger
    {
        Utils.TaskLoggingHelper log;
        public MSBuildLogger(Utils.TaskLoggingHelper log)
        {
            this.log = log;
        }

        public bool ReturnValue = false;
        public void Initalize(Logger logger)
        {
            logger.BeginAssembly += BeginAssembly;
            logger.EndAssembly += EndAssembly;
            logger.Phase += BeginPhase;
            logger.Log += Logging;
            logger.Progress += Progressing;
            logger.Fault += Fault;
            logger.End += End;
        }

        void BeginAssembly(object sender, AssemblyEventArgs e)
        {
            log.LogMessage(MessageImportance.Normal, "Processing '{0}'...", e.Assembly.FullName);
        }
        void EndAssembly(object sender, AssemblyEventArgs e)
        {
            log.LogMessage(MessageImportance.Low, "End processing '{0}'.", e.Assembly.FullName);
        }
        void BeginPhase(object sender, LogEventArgs e)
        {
            log.LogMessage(MessageImportance.Low, e.Message);
        }
        void Logging(object sender, LogEventArgs e)
        {
            log.LogMessage(MessageImportance.Low, e.Message);
        }
        void Progressing(object sender, ProgressEventArgs e)
        {
            //
        }
        void Fault(object sender, ExceptionEventArgs e)
        {
            log.LogError("Confuser", "CR003", "Confuser",
                0, 0, 0, 0, string.Format(@"***************
ERROR IN CONFUSER!!
Message : {0}
Stack Trace : {1}
***************", e.Exception.Message, e.Exception.StackTrace));
            ReturnValue = false;
        }
        void End(object sender, LogEventArgs e)
        {
            log.LogMessage(@"***************
SUCCEEDED!!
{0}
***************", e.Message);
            ReturnValue = true;
        }
    }
}
