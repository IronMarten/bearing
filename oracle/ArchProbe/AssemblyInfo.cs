using System.Runtime.CompilerServices;

// The probe's model and analysis types are internal. The regression suite asserts against
// them directly rather than against console text, which is the whole point of Phase 0:
// the sentences in Report.cs are being replaced, so nothing may depend on their wording.
[assembly: InternalsVisibleTo("Bearing.Tests")]
