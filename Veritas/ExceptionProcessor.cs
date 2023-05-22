using VSharp.Interpreter.IL;

namespace Veritas;

public class ExceptionInfo
{
    public Type Type { get; }

    public List<PointInfo> StackTrace { get; } = new();

    public ExceptionInfo(ISequencePointsIndex index, exceptionInfo info)
    {
        Type = info.exceptionType;
        StackTrace = new List<PointInfo>(info.stackTrace.Count());
        foreach (var cl in info.stackTrace)
        {
            var p = index.FindPoint(cl);
            if (p != null)
            {
                StackTrace.Add(p);
            }
        }
    }
}

public class ExceptionProcessor
{
    private List<ExceptionInfo> _exceptions;

    public ExceptionProcessor(ISequencePointsIndex index, IEnumerable<exceptionInfo> exceptions)
    {
        _exceptions = exceptions.Select(e => new ExceptionInfo(index, e)).ToList();
    }

    private bool TargetHasExceptionType(Target target, ExceptionInfo ex)
    {
        if (target.Issue == hypothesisType.NullDereference)
        {
            return ex.Type == typeof(NullReferenceException);
        }

        if (target.Issue == hypothesisType.IndexOutOfRange)
        {
            return ex.Type == typeof(IndexOutOfRangeException);
        }

        return false;
    }

    public Dictionary<Target, List<ExceptionInfo>> GetProvedTargets(List<Target> targets)
    {
        var result = new Dictionary<Target, List<ExceptionInfo>>();
        foreach (var t in targets)
        {
            var loc = t.Result.Locations[0].PhysicalLocation;
            var file = loc.ArtifactLocation.Uri.AbsolutePath;
            var line = loc.Region.StartLine - 2;

            foreach (var e in _exceptions)
            {
                var stackTraceHasTarget = e.StackTrace.Any(l => l.FileName == file && l.StartLine == line);
                var targetHasExceptionType = TargetHasExceptionType(t, e);

                if (targetHasExceptionType && stackTraceHasTarget)
                {
                    if (!result.ContainsKey(t))
                    {
                        result[t] = new List<ExceptionInfo>();
                    }
                    result[t].Add(e);
                }
            }
        }
        return result;
    }
}
