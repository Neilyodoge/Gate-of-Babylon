using System.Collections.Generic;
using Edgar.Unity.Diagnostics;

namespace Edgar.Unity
{
    /// <summary>
    /// This exception is used when the generator is not able to produce an output in a given time.
    /// </summary>
    public class TimeoutException : GeneratorException
    {
        public List<IDiagnosticResult> DiagnosticResults { get; set; }

        public TimeoutException() : base("生成器未能在规定时间内生成关卡，请查看控制台上方的诊断信息。")
        {
            /* empty */
        }
    }
}