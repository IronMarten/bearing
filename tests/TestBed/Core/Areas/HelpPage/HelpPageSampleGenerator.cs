namespace TestBed.Core.Areas.HelpPage;

// Scaffolding, verbatim in spirit from ASP.NET Web API HelpPage. Exists only to prove the
// path exclusion works — it must NOT appear in output unless --no-default-excludes.
public class HelpPageSampleGenerator
{
    public Dictionary<string, object> ActualHttpMessageTypes { get; set; } = new();

    public object GetSample(string controllerName, string actionName, int sampleDirection)
    {
        if (controllerName == null) return null;
        if (actionName == null) return null;
        switch (sampleDirection)
        {
            case 0: return ActualHttpMessageTypes.TryGetValue(controllerName, out var a) ? a : null;
            case 1: return ActualHttpMessageTypes.TryGetValue(actionName, out var b) ? b : null;
            case 2: return ActualHttpMessageTypes.Count > 0 ? ActualHttpMessageTypes.First().Value : null;
            default: return null;
        }
    }
}

public static class HelpPageConfigurationExtensions
{
    public static string SampleObjects(object config) => config?.ToString() ?? "";
}
